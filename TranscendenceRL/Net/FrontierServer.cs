﻿using ArchConsole;
using Common;
using NetCoreServer;
using SadConsole;
using SadConsole.Input;
using static SadConsole.Input.Keys;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Console = SadConsole.Console;
using Debug = System.Diagnostics.Debug;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TranscendenceRL {
    class FrontierSession : TcpSession {
        private ScreenServer game;
        private PlayerShip playerShip;
        private AIShip removed;

        private MemoryStream received;
        private CommandServer command;
        private int length;
        public FrontierSession(TcpServer server, ScreenServer game) : base(server) {
            this.game = game;
        }
        protected override void OnConnected() {

            while (game.busy) {
                Thread.Yield();
            }
            game.requests++;
            //var s = Common.Space.Zip(SaveGame.Serialize(game.World));
            var s = (SaveGame.Serialize(game.World));
            game.requests--;
            SendCommand(CommandClient.SET_WORLD, s);
        }
        public void SendCommand(CommandClient command, string s) {
            Send($"{Enum.GetName(command)}{s.Length}");
            Send(s);
        }
        private void RemovePlayer() {
            if (playerShip != null) {
                game.World.RemoveEntity(playerShip);
                game.playerControls.Remove(playerShip);
            }
            if (removed != null) {
                game.World.AddEntity(removed);
            }
        }
        protected override void OnDisconnected() {
            RemovePlayer();
        }
        protected override void OnReceived(byte[] buffer, long offset, long size) {
            var str = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            var m = Regex.Match(str, "([A-Z_]+)([0-9]+)");
            if (m.Success) {
                received = new MemoryStream();
                command = Enum.Parse<CommandServer>(m.Groups[1].Captures[0].Value);
                length = int.Parse(m.Groups[2].Captures[0].Value);
            } else {
                received.Write(buffer, (int)offset, (int)size);
                if (received.Length >= length) {
                    //var str = Space.Unzip(received);
                    str = Encoding.UTF8.GetString(received.ToArray());
                    //var d = SaveGame.Deserialize(str);
                    switch (command) {
                        case CommandServer.PLAYER_SHIP_ASSUME:
                            int Id = int.Parse(str);
                            var ai = (AIShip)game.entityLookup[Id];
                            var World = game.World;
                            removed = ai;
                            World.RemoveEntity(ai);
                            playerShip = new PlayerShip(new Player(new Settings()), ai.ship);
                            World.AddEntity(playerShip);
                            break;
                        case CommandServer.PLAYER_SHIP_LEAVE:
                            RemovePlayer();
                            break;
                        case CommandServer.PLAYER_SHIP_INPUT:
                            if(playerShip == null) {
                                return;
                            }
                            var input = SaveGame.Deserialize<PlayerInput>(str);
                            input.ServerOnly();
                            game.playerControls[playerShip] = input;
                            break;
                    }
                    received.Close();
                }
            }
        }
        protected override void OnError(SocketError error) =>
            Debug.WriteLine($"Chat TCP session caught an error with code {error}");
    }
    public class FrontierServer : TcpServer {
        public ScreenServer game;
        public FrontierServer(IPAddress address, int port, ScreenServer game) : base(address, port) {
            this.game = game;
        }
        public void MulticastCommand(CommandClient command, string s) {
            Multicast($"{Enum.GetName(command)}{s.Length}");
            Multicast(s);
        }
        protected override TcpSession CreateSession() => new FrontierSession(this, game);
        protected override void OnError(SocketError error) =>
            Debug.WriteLine($"Chat TCP server caught an error with code {error}");
    }


    public class ScreenServer : Console {
        public TitleScreen prev { get; }
        public World World;
        public SpaceObject pov;
        public XY camera;
        public MouseWatch mouse { get; } = new();
        public Dictionary<(int, int), ColoredGlyph> tiles { get; } = new();
        public Dictionary<int, Entity> entityLookup = new();
        public Dictionary<PlayerShip, PlayerInput> playerControls = new();

        public int requests;
        public bool busy;
        private FrontierServer server;
        public ScreenServer(int width, int height, TitleScreen prev) : base(width, height) {
            this.prev = prev;
            this.World = prev.World;
            camera = prev.camera;

            UseKeyboard = true;

            server = new FrontierServer(IPAddress.Any, 1111, this);
            server.Start();
            UpdateUI();
        }

        public void UpdateUI() {
            var fs = FontSize * 2;
            Children.Clear();
            LabelButton clientButton = null;

            void UpdateText() {
                clientButton.text = server.IsAccepting ? "Online" :
                    server.IsStarted ? "Running" : "Offline";
            }
            void StartServer() {
                if (server.IsAccepting) {
                    Task.Run(() => {
                        server.Stop();
                        UpdateText();
                    });
                } else if (server.IsStarted) {
                    Task.Run(() => {
                        server.Stop();
                        UpdateText();
                    });
                } else {
                    Task.Run(() => {
                        server.Start();
                        UpdateText();
                    });
                }
                UpdateText();
            }

            clientButton = new LabelButton("",
                () => StartServer()) { Position = new Point(1, 1), FontSize = fs };
            Children.Add(clientButton);

            UpdateText();
        }
        public void StartServer() {
            if (server.IsStarted) {
                server.Stop();
            } else {
                server.Start();
            }
        }

        public override void Update(TimeSpan timeSpan) {
            if (requests > 0) {
                return;
            }

            playerControls.UpdatePlayerControls();

            busy = true;
            World.UpdateAdded();
            World.UpdateActive();
            World.UpdateRemoved();
            busy = false;

            entityLookup.UpdateEntityLookup(World);
            if (World.tick % 90 == 0) {
                List<Entity> entityAtlas = new List<Entity>();
                foreach (var e in World.entities.all) {
                    var en = e.GetSharedState();
                    if(en != null) {
                        entityAtlas.Add(en);
                    }
                }
                server.MulticastCommand(CommandClient.ENTITY_ENFORCE_CERTAINTY, SaveGame.Serialize(entityAtlas));
            }

            tiles.Clear();
            World.PlaceTiles(tiles);

            if (pov == null) {
                pov = (SpaceObject)playerControls.Keys.FirstOrDefault() ?? (SpaceObject)World.entities.all.OfType<AIShip>().FirstOrDefault();
                return;
            }
            //Smoothly move the camera to where it should be
            if ((camera - pov.position).magnitude < pov.velocity.magnitude / 15 + 1) {
                camera = pov.position;
            } else {
                var step = (pov.position - camera) / 15;
                if (step.magnitude < 1) {
                    step = step.normal;
                }
                camera += step;
            }
        }
        public override void Render(TimeSpan drawTime) {
            this.Clear();

            for (int x = 0; x < Width; x++) {
                for (int y = 0; y < Height; y++) {
                    var g = this.GetGlyph(x, y);

                    var offset = new XY(x, Height - y) - new XY(Width / 2, Height / 2);
                    var location = camera + offset;
                    if (g == 0 || g == ' ' || this.GetForeground(x, y).A == 0) {


                        if (tiles.TryGetValue(location.roundDown, out var tile)) {
                            if (tile.Background == Color.Transparent) {
                                tile.Background = World.backdrop.GetBackground(location, camera);
                            }
                            this.SetCellAppearance(x, y, tile);
                        } else {
                            this.SetCellAppearance(x, y, World.backdrop.GetTile(location, camera));
                        }
                    } else {
                        this.SetBackground(x, y, World.backdrop.GetBackground(location, camera));
                    }

                }
            }
            base.Render(drawTime);
        }
        public override bool ProcessMouse(MouseScreenObjectState state) {


            mouse.Update(state, IsMouseOver);
            mouse.nowPos = new Point(mouse.nowPos.X, Height - mouse.nowPos.Y);
            if (mouse.left == ClickState.Held) {
                camera += new XY(mouse.prevPos - mouse.nowPos);
                server.MulticastCommand(CommandClient.SET_CAMERA, SaveGame.Serialize(camera));
            }

            return base.ProcessMouse(state);

        }
        public override bool ProcessKeyboard(Keyboard info) {
            if (info.IsKeyPressed(Keys.K)) {
                if (pov.active) {
                    pov.Destroy(pov);
                }
            }


            if (info.IsKeyPressed(Escape)) {
                server.Stop();
                prev.pov = null;
                prev.camera = camera;
                SadConsole.Game.Instance.Screen = prev;
                prev.IsFocused = true;
            }
            return base.ProcessKeyboard(info);
        }
    }
}
