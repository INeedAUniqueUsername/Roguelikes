﻿using Common;
using SadConsole;
using SadConsole.Input;
using SadConsole.UI;
using System;
using SadRogue.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SadConsole.Input.Keys;
using Console = SadConsole.Console;
using SadConsole.UI.Controls;
using ASECII;
using TranscendenceRL.Screens;

namespace TranscendenceRL {

    class ShipSelectorModel {
        public World World;
        public List<ShipClass> playable;
        public int shipIndex;

        public List<GenomeType> genomes;
        public int genomeIndex;

        public string playerName;
        public GenomeType playerGenome;
    }
    class PlayerCreator : ControlsConsole {
        private Console prev;
        private ShipSelectorModel context;

        private ref World World => ref context.World;
        private ref List<ShipClass> playable => ref context.playable;
        private ref int index => ref context.shipIndex;
        private ref List<GenomeType> genomes => ref context.genomes;
        private ref int genomeIndex => ref context.genomeIndex;
        private ref GenomeType playerGenome => ref context.playerGenome;

        double time = 0;
        public PlayerCreator(int width, int height, Console prev, World World) : base(width, height) {
            this.prev = prev;
            DefaultBackground = Color.Black;
            DefaultForeground = Color.White;

            context = new ShipSelectorModel() {
                World = World,
                playable = World.types.shipClass.Values.Where(sc => sc.playerSettings?.startingClass == true).ToList(),
                shipIndex = 0,
                genomes = World.types.genomeType.Values.ToList(),
                genomeIndex = 0,
                playerName = "Luminous",
                playerGenome = World.types.genomeType.Values.First()
            };

            int y = 2;

            var nameField = new LabeledControl("Name           ", context.playerName) { Position = new Point(0, y) };
            nameField.textBox.TextChanged += (e, s) => context.playerName = nameField.textBox.Text;
            this.Children.Add(nameField);

            y++;

            Label identityLabel = new Label("Identity       ") { Position = new Point(0, y)};
            this.Children.Add(identityLabel);

            LabelButton identityButton = null;
            double lastClick = 0;
            int fastClickCount = 0;
            identityButton = new LabelButton(playerGenome.name, () => {
                if(time - lastClick > 0.5) {
                    genomeIndex = (genomeIndex + 1) % genomes.Count;
                    playerGenome = genomes[genomeIndex];
                    identityButton.text = playerGenome.name;
                    fastClickCount = 0;
                } else {
                    fastClickCount++;
                    if(fastClickCount > 3) {
                        this.Children.Remove(identityLabel);
                        this.Children.Remove(identityButton);

                        context.playerGenome = new GenomeType() {
                            name = "Human Variant",
                            species = "human",
                            gender = "variant",
                            subjective = "they",
                            objective="them",
                            possessiveAdj="their",
                            possessiveNoun="theirs",
                            reflexive="theirself"
                        };
                        this.Children.Add(new LabeledControl("Identity       ", playerGenome.name, s => playerGenome.name = s) { Position = new Point(0, y++)});
                        this.Children.Add(new LabeledControl("Species        ", playerGenome.species, s => playerGenome.species = s) { Position = new Point(0, y++) });
                        this.Children.Add(new LabeledControl("Gender         ", playerGenome.gender, s => playerGenome.gender = s) { Position = new Point(0, y++) });
                        this.Children.Add(new LabeledControl("Subjective     ", playerGenome.subjective, s => playerGenome.subjective = s) { Position = new Point(0, y++) });
                        this.Children.Add(new LabeledControl("Objective      ", playerGenome.objective, s => playerGenome.objective = s) { Position = new Point(0, y++) });
                        this.Children.Add(new LabeledControl("Possessive Adj.", playerGenome.possessiveAdj, s => playerGenome.possessiveAdj = s) { Position = new Point(0, y++) });
                        this.Children.Add(new LabeledControl("Possessive Noun", playerGenome.possessiveNoun, s => playerGenome.possessiveNoun = s) { Position = new Point(0, y++) });
                        this.Children.Add(new LabeledControl("Reflexive      ", playerGenome.reflexive, s => playerGenome.reflexive = s) { Position = new Point(0, y++) });
                    }
                }
                lastClick = time;


            }) { Position = new Point(16, y) };
            this.Children.Add(identityButton);
        }
        public override void Update(TimeSpan delta) {
            time += delta.TotalSeconds;
            base.Update(delta);
        }
        public override void Draw(TimeSpan drawTime) {
            this.Clear();

            var current = playable[index];

            int shipDescY = 12;


            string leftArrow = "<===  [Left Arrow]";
            this.Print(Width / 4 - leftArrow.Length - 1, shipDescY, leftArrow, index > 0 ? Color.White : Color.Gray);
            string rightArrow = "[Right Arrow] ===>";
            this.Print(Width * 3 / 4 + 1, shipDescY, rightArrow, index < playable.Count - 1 ? Color.White : Color.Gray);

            shipDescY++;
            shipDescY++;

            var nameX = Width / 4 - current.name.Length / 2;
            var y = shipDescY;
            this.Print(nameX, y, current.name);

            var map = current.playerSettings.map;
            var mapWidth = map.Select(line => line.Length).Max();
            var mapX = Width/4 - mapWidth/2;
            y++;
            foreach(var line in current.playerSettings.map) {
                for(int i = 0; i < line.Length; i++) {
                    this.SetCellAppearance(mapX + i, y, new ColoredGlyph(new Color(255, 255, 255, 230 + (int)(Math.Sin(time*1.5 + Math.Sin(i)*5 + Math.Sin(y)*5)*25)), Color.Black, line[i]));
                }
                //this.Print(mapX, y, line);
                y++;
            }

            string s = "[Image is for promotional use only]";
            var strX = Width/4 - s.Length / 2;
            this.Print(strX, y, s);

            var descX = Width * 2 / 4;
            y = shipDescY;
            foreach(var line in current.playerSettings.description.Wrap(Width/3)) {
                this.Print(descX, y, line);
                y++;
            }

            y++;

            //Show installed devices on the right pane
            this.Print(descX, y, "Installed Devices:");
            y++;
            foreach (var device in current.devices.Generate(World.types)) {
                this.Print(descX+4, y, device.source.type.name);
                y++;
            }

            string start = "[Enter] Start";
            this.Print(Width - start.Length, Height - 1, start);

            for(y = 0; y < Height; y++) {
                for(int x = 0; x < Width; x++) {

                    var g = this.GetGlyph(x, y);
                    if (g == 0 || g == ' ') {
                        this.SetCellAppearance(x, y, new ColoredGlyph(
                            new Color(255, 255, 255, (int)(51 * Math.Sin(time * Math.Sin(x - y) + Math.Sin(x) * 5 + Math.Sin(y) * 5))),
                            Color.Black,
                            '='));
                    }
                }
            }

            base.Draw(drawTime);
        }
        public override bool ProcessKeyboard(Keyboard info) {
            if(info.IsKeyPressed(Right) && index < playable.Count - 1) {
                index = (index+1)%playable.Count;
            }
            if(info.IsKeyPressed(Left) && index > 0) {
                index = (playable.Count + index - 1) % playable.Count;
            }
            if(info.IsKeyPressed(Escape)) {
                IsFocused = false;
                SadConsole.Game.Instance.Screen = new TitleSlide(Width, Height, prev) { IsFocused = true };
            }
            if(info.IsKeyPressed(Enter)) {
                SadConsole.Game.Instance.Screen = new CrawlScreen(Width, Height, World, playable[index]) { IsFocused = true };
            }
            return base.ProcessKeyboard(info);
        }
    }
}
