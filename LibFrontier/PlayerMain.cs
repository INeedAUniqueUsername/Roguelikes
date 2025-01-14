﻿using Common;
using static RogueFrontier.PlayerShip;
using System.Reflection;
using LibGamer;
using System.Xml.Linq;
using RogueFrontier;
using System.Data;
using LabelButton = LibGamer.SfLink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
namespace RogueFrontier;
public record Monitor (int Width, int Height, World world, PlayerShip playerShip, Camera camera) {
	public Monitor FreezeCamera => this with { camera = new(playerShip.position) };
}
public class Mainframe : IScene, Ob<PlayerShip.Destroyed> {
    public Action<IScene> Go { set; get; }
    public Action<Sf> Draw { set; get; }
    public Action<SoundCtx> PlaySound { set; get; }
    public Action SetFocus = () => { };
	public void Observe(PlayerShip.Destroyed ev) {
        var (p, d, w) = ev;
        OnPlayerDestroyed($"Destroyed by {d?.name ?? "unknown forces"}", w);
    }
    public ShipControls Settings;
    public Sf sf;
    public int Width => sf.GridWidth;
    public int Height => sf.GridHeight;
    public World world => playerShip.world;
    public Camera camera { get; private set; }
	public Profile profile;
    public Timeline story;
    public PlayerShip playerShip;
    public PlayerControls playerControls;
    public Noisemaker audio;
    public XY mouseWorldPos;
    public bool sleepMouse = true;
    public BackdropConsole uiBack;
    public Viewport uiViewport;
    public GateTransition gate {
        get => _gate;
        set {
            if(_gate is { } prev) {
                prev.Draw -= SubDraw;
            }
            _gate = value;
            if(value is { } next) {
                next.Draw += SubDraw;
            }

		}
    }
    private GateTransition _gate;
    public Megamap uiMegamap;
    public Vignette vignette;
    public Readout uiMain;  //If this is visible, then all other ui Consoles are visible
    public Edgemap uiEdge;
    public Minimap uiMinimap;
    public PowerWidget powerWidget;
    public ListWidget<Item> invokeWidget;
    public PauseScreen pauseScreen;
    public GalaxyMap networkMap;
    private TargetingMarker crosshair;
    private double targetCameraRotation;
    public bool autopilotUpdate;
    //public bool frameRendered = true;
    public int updatesSinceRender = 0;
    private ListTracker<World> systems;
    public SoundCtx music;
    public World silenceSystem;
    public Monitor monitor;
    public Hand mouse = new();
    public IScene dialog {
        get => _dialog;
        set {
            if(dialog is { } _d) {
                dialog.Go -= SubGo;
                dialog.Draw -= SubDraw;
                dialog.PlaySound -= SubPlaySound;
			}
            if(value == this) {
                _dialog = null;
                return;
            }
            _dialog = value;
            if(_dialog is { } d) {
				d.Go += SubGo;
				d.Draw += SubDraw;
				d.PlaySound += SubPlaySound;
			}
        }
    }
	private void SubGo (IScene s) => dialog = s;
    private void SubDraw (Sf s) => Draw?.Invoke(s);
    private void SubPlaySound (SoundCtx s) => PlaySound?.Invoke(s);
	private IScene _dialog;
    public Mainframe(int Width, int Height, Profile profile, PlayerShip playerShip) {
        sf = new(Width, Height, Fonts.FONT_8x8);
        camera = new();
        this.profile = profile;
        this.story = new(playerShip);
        this.playerShip = playerShip;
        this.playerControls = new(playerShip, this);
        Settings = ShipControls.standard;
        silenceSystem = new World(world.universe);
        var tc = silenceSystem.types;
        silenceSystem.AddEntity(playerShip);

        IWeaponListener silenceListener = new SilenceListener(silenceSystem);
        foreach(var e in world.universe.GetAllEntities()) {
            var owner = e;
            if(owner is ISegment s) {
                owner = s.parent;
            }
            if(owner is Station st && st.type.attributes.Contains("Murmurs")) {
                silenceSystem.AddEntity(e);
                silenceListener.Register(st);
            }
        }
        silenceListener.Register(playerShip);

		monitor = new(Width, Height, world, playerShip, camera);

		audio = new(playerShip);
        audio.Register(playerShip.world.universe);
        audio.PlaySound += SubPlaySound;

        uiBack = new(monitor);
        uiViewport = new(Width, Height, monitor);
        uiMegamap = new(monitor, world.backdrop.layers.Last());
        vignette = new(this);
        uiMain = new(monitor);
        uiEdge = new(monitor);
        uiMinimap = new(monitor);

		void DrawPar (Sf sf) {
			Draw?.Invoke(sf);
		}
		powerWidget = new(this) { visible = false };

		pauseScreen = new(this) { visible = false };
        networkMap = new(this) { visible = false };
        crosshair = new(playerShip, "Mouse Cursor", new());
        systems = new([..playerShip.world.universe.systems.Values]);
    }
    public void SleepMouse() => sleepMouse = true;
    public void HideUI() {
        uiMain.visible = false;
    }
    public void ShowUI() {
        uiMain.visible = true;
    }
    public void HideAll() {
        //Force exit any scenes
        dialog = null;
        //Force exit power menu
        powerWidget.visible = false;
        uiMain.visible = false;

        //Pretty sure this can't happen but make sure
        pauseScreen.visible = false;
    }
    public void Jump() {
        var prevViewport = new Viewport(Width, Height, monitor.FreezeCamera);
        var nextViewport = new Viewport(Width, Height, monitor);
        uiBack = new(nextViewport);
        uiViewport = nextViewport;
        gate = new GateTransition(prevViewport, nextViewport, () => {
            gate = null;
        });
    }
    public void Gate() {
        if (!playerShip.CheckGate(out var exit))
            return;
        var destGate = exit.destGate;
        if (destGate == null) {
            world.entities.Remove(playerShip);
            gate = new GateTransition(new Viewport(Width, Height, monitor.FreezeCamera), null, () => {
                gate = null;
				OnPlayerLeft();
            });
            return;
        }
		var prevViewport = new Viewport(Width, Height, monitor.FreezeCamera);
		world.entities.Remove(playerShip);
        var dest = destGate.world;
		dest.entities.Add(playerShip);
		dest.effects.Add(new Heading(playerShip));
		playerShip.ship.world = dest;
        playerShip.ship.position = destGate.position + (playerShip.ship.position - exit.position);

		monitor = monitor with { world = dest };
		var nextViewport = new Viewport(Width, Height, monitor);
		gate = new GateTransition(prevViewport, nextViewport, () => {
            gate = null;
        });
		uiViewport = nextViewport;
		uiBack = new(nextViewport);
	}
    public void OnPlayerLeft() {
        HideAll();
        world.entities.Remove(playerShip);
        var oc = default(OutroCrawl);
        oc = new OutroCrawl(Width, Height, EndGame);
		Go(oc);
        void EndGame() {
            oc.Go(new EpitaphScreen(this, new() { desc = $"Left Human Space", deathFrame = null, wreck = null }));
        }
    }
    public void OnPlayerDestroyed(string message, Wreck wreck) {
        //Clear mortal time so that we don't slow down after the player dies
        wreck.cargo.Clear();
        playerShip.mortalTime = 0;
        playerShip.ship.blindTicks = 0;
        playerShip.ship.silence = 0;
        HideAll();
        //Get a snapshot of the player
        var size = sf.GridHeight;
        var deathFrame = new Tile[size, size];
        var center = new XY(size / 2, size / 2);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                var tile = GetTile(camera.position + new XY(-x, -y) + center);
                deathFrame[size - x - 1, y] = tile;
            }
        }
        Tile GetTile(XY xy) {
            var back = world.backdrop.GetTile(xy, camera.position);
            //Round down to ensure we don't get duplicated tiles along the origin
            if (uiViewport.tiles.TryGetValue(xy.roundDown, out var g)) {
                return g with { Background = ABGR.Blend(ABGR.Premultiply(back.Background), g.Background) };
            } else {
                return back;
            }
        }
        var ep = new Epitaph { desc = message, deathFrame = deathFrame, wreck = wreck };
        playerShip.person.Epitaphs.Add(ep);
        playerShip.autopilot = false;
        //Bug: Background is not included because it is a separate console
        var ds = new EpitaphScreen(this, ep);
        var dt = new DeathTransition(this, sf, ds);
        var dp = new DeathPause(this, dt);

        Go?.Invoke(dp);
        Task.Run(() => {
            lock (world) {
                StreamWriter w = null;
                try {
#if false
                    new DeadGame(world, playerShip, ep).Save();
#else
                    //Task.Delay(2000);
                    Thread.Sleep(2000);
#endif
                } catch(Exception e) {
#if !DEBUG
                    throw;
#else
                    if (w == null) {
                        var name = $"[{DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")}] Save Failed.txt";
                        w = new(new FileStream(name, FileMode.Create));
                    }
                    w.Write(e.Message);
#endif
                }
                w?.Close();
            }
            dp.done = true;
        });
    }
    public void UpdateClient(TimeSpan delta) {
        //if(!frameRendered) return;
        if (updatesSinceRender > 2) return;
        updatesSinceRender++;
        void UpdateUniverse() {
            world.UpdateActive(delta.TotalSeconds * (playerShip.autopilot ? 3 : 1));
            world.UpdatePresent();
            story.Update(playerShip);
        }
        if (true) {
            uiBack.Update(delta);
            lock (world) {
                UpdateUniverse();
                PlaceTiles(delta);
                gate?.Update(delta);
            }
            if (playerShip.dock is { justDocked: true, Target: IDockable d } dock) {
                audio.PlayDocking(false);
                var scene =
                    story.GetScene(new(this), d) ??
                    d.GetDockScene(new(this));
                if (scene != null) {
                    playerShip.DisengageAutopilot();
                    dock.Clear();
                    SubGo(new ScanTransition(scene, sf));
                } else {
                    playerShip.AddMessage(new Message($"Stationed on {dock.Target.name}"));
                }
            }
        }
        camera.position = playerShip.position;
        //frameRendered = false;
        //Required to update children
    }
    public void Update(TimeSpan delta) {
        //if(!frameRendered) return;
        if (updatesSinceRender > 2) return;
        updatesSinceRender++;
        if (pauseScreen.visible) {
            pauseScreen.Update(delta);
            return;
        }
        if (networkMap.visible) {
            //networkMap.Update(delta);
            return;
        }
        var gameDelta = delta.TotalSeconds * (playerShip.autopilot ? 3 : 1) * Max(0, 1 - playerShip.ship.silence);
        if(playerShip is { active:true, mortalTime: > 0 } ps) {
            ps.mortalTime -= gameDelta;
            gameDelta /= (1 + ps.mortalTime/2);
        }
        void UpdateUniverse() {
            //playerShip.updated = false;
            world.UpdatePresent();
            world.UpdateActive(gameDelta);
            
            silenceSystem.UpdatePresent();
            silenceSystem.UpdateActive(delta.TotalSeconds * Min(1, playerShip.ship.silence));
            
            systems.GetNext(1).ForEach(s => {
                if (s != world) {
                    s.UpdateActive(gameDelta);
                    s.UpdatePresent();
                }
            });
        }
        if (gameDelta > 0) {
            uiBack.Update(delta);
            lock (world) {
                //Need to fix this for silence system
                if (dialog == null) {
                    playerControls.ProcessAll();
                    playerControls.input = new();
                }
                audio.Update(delta.TotalSeconds);
                AddCrosshair();
                UpdateUniverse();
                playerShip.ResetActiveControls();
                PlaceTiles(delta);
                gate?.Update(delta);
                void AddCrosshair() {
                    if (playerShip.GetTarget(out var t) && t == crosshair) {
                        Heading.Crosshair(world, crosshair.position, ABGR.Yellow);
                    }
                }
            }
            if (playerShip.dock is {  justDocked:true, Target: IDockable d } dock) {
                audio.PlayDocking(false);
                var scene = story.GetScene(new(this), d) ?? d.GetDockScene(new(this));
                if (scene != null) {
                    playerShip.DisengageAutopilot();
                    dock.Clear();
                    SubGo(new ScanTransition(scene, sf));
                } else {
                    playerShip.AddMessage(new Message($"Stationed on {dock.Target.name}"));
                }
            }
        }
        UpdateUI(delta);
        camera.position = playerShip.position;
        playerControls.input = new();
        //frameRendered = false;
        //Required to update children
    }
    public void UpdateUI(TimeSpan delta) {
        var d = Main.AngleDiffRad(camera.rotation, targetCameraRotation);
        if (Abs(d) < 0.01) {
            camera.rotation += d;
        } else {
            camera.rotation += d / 10;
        }
        if (dialog is { }di) {
            di.Update(delta);
        } else {
            if (uiMain.visible) {
                uiMegamap.Update(delta);

                vignette.Update(delta);

                uiMain.viewScale = uiMegamap.viewScale;
                uiMain.Update(delta);

                uiEdge.viewScale = uiMegamap.viewScale;
                uiEdge.Update(delta);

                uiMinimap.alpha = (byte)(255 - uiMegamap.alpha);
                uiMinimap.Update(delta);
            } else {
                uiMegamap.Update(delta);
                vignette.Update(delta);
            }
            if (powerWidget.visible) {
                powerWidget.Update(delta);
            }
        }
    }
    public void PlaceTiles(TimeSpan delta) {
        if (playerShip.ship.blindTicks > 0) {
            uiViewport.UpdateBlind(delta, playerShip.GetVisibleDistanceLeft);
        } else {
            uiViewport.UpdateVisible(delta, playerShip.GetVisibleDistanceLeft);
        }
        /*
        foreach((var key, var value) in viewport.tiles) {
            viewport.tiles[key] = new(value.Foreground, value.Background, '?');
        }
        */
    }
    public void RenderWorld(TimeSpan delta) {
        uiViewport.Render(delta);
    }
    public void Render(TimeSpan delta) {
        if (pauseScreen.visible) {
            uiBack.Render(delta);       Draw(uiBack.sf);
            uiViewport.Render(delta);   Draw(uiViewport.sf);
            vignette.Render(delta);     Draw(vignette.sf);
            pauseScreen.Render(delta);  Draw(pauseScreen.sf);
        } else if (networkMap.visible) {
            networkMap.Render(delta);   Draw(networkMap.sf);
        } else if (dialog != null) {
            uiBack.Render(delta);       Draw(uiBack.sf);
            uiViewport.Render(delta);   Draw(uiViewport.sf);
            vignette.Render(delta);     Draw(vignette.sf);
            dialog.Render(delta);
        } else if(playerShip.active) {
            //viewport.UsePixelPositioning = true;
            //viewport.Position = (playerShip.position - playerShip.position.roundDown) * 8 * new XY(1, -1) * -1;
            if (uiMain.visible) {
                //If the megamap is completely visible, then skip main render so we can fast travel
                if (uiMegamap.alpha < 255) {
                    if (gate != null) {
                        gate.Render(delta);
                    } else {
                        uiBack.Render(delta);           Draw(uiBack.sf);
                        uiViewport.Render(delta);       Draw(uiViewport.sf);
                    }
                    uiMegamap.Render(delta);    Draw(uiMegamap.sf);

                    vignette.Render(delta);     Draw(vignette.sf);

					uiMain.Render(delta);       Draw(uiMain.sf_ui);
					uiEdge.Render(delta);       Draw(uiEdge.sf);
                    uiMinimap.Render(delta);    Draw(uiMinimap.sf);
                } else {
                    uiMegamap.Render(delta);    Draw(uiMegamap.sf);
                    vignette.Render(delta);     Draw(vignette.sf);
                    uiMain.Render(delta);       Draw(uiMain.sf_ui);
                    uiEdge.Render(delta);       Draw(uiEdge.sf);
                }
            } else {
                /*
                if (transition != null) {
                    transition.Render(drawTime);
                } else {
                    back.Render(drawTime);
                    viewport.Render(drawTime);
                }
                vignette.Render(drawTime);
                */

                //If the megamap is completely visible, then skip main render so we can fast travel
                if (uiMegamap.alpha < 255) {
                    if (gate != null) {
                        gate.Render(delta);
                    } else {
                        uiBack.Render(delta);       Draw?.Invoke(uiBack.sf);
                        uiViewport.Render(delta);   Draw?.Invoke(uiViewport.sf);
                    }
                    uiMegamap.Render(delta);        Draw?.Invoke(uiMain.sf_ui);
                    vignette.Render(delta);         Draw?.Invoke(vignette.sf);
                } else {
                    uiMegamap.Render(delta);        Draw?.Invoke(uiMegamap.sf);
                    vignette.Render(delta);         Draw?.Invoke(vignette.sf);
                }
            }
            if (powerWidget.visible) {
                powerWidget.Render(delta);      Draw?.Invoke(powerWidget.sf);
            }
        } else {
            uiBack.Render(delta);           Draw?.Invoke(uiBack.sf);
            uiViewport.Render(delta);       Draw?.Invoke(uiViewport.sf);
        }
        //frameRendered = true;
        updatesSinceRender = 0;
    }
    public void HandleKey(KB kb) {
		if (dialog is { }dl) {
            dl.HandleKey(kb);
            return;
        }
        uiMegamap.HandleKey(kb);
        /*
        if (uiMain.IsVisible) {
            uiMegamap.ProcessKeyboard(info);
        }
        */
#if false
        if (info.IsKeyPressed(N)) {
            galaxyMap.IsVisible = !galaxyMap.IsVisible;
        }
#endif
        //Intercept the alphanumeric/Escape keys if the power menu is active
        if (pauseScreen.visible) {
            pauseScreen.HandleKey(kb);
        } else if (networkMap.visible) {
            networkMap.HandleKey(kb);
        } else if (powerWidget.visible) {
            playerControls.UpdateInput(kb);
            powerWidget.HandleKey(kb);
        } else {
            playerControls.UpdateInput(kb);
            var p = kb.IsPress;
            var d = kb.IsDown;
            if (d(KC.LeftShift) || d(KC.RightShift)) {
                if (p(KC.OemOpenBrackets)) {
                    targetCameraRotation += PI / 2;
                }
                if (p(KC.OemCloseBrackets)) {
                    targetCameraRotation -= PI / 2;
                }
            } else {
                if (d(KC.OemOpenBrackets)) {
                    camera.rotation += 0.01;
                    targetCameraRotation = camera.rotation;
                }
                if (d(KC.OemCloseBrackets)) {
                    camera.rotation -= 0.01;
                    targetCameraRotation = camera.rotation;
                }
            }
        }
    }
    public void HandleMouse(HandState state) {
        mouse.Update(state);
        if (pauseScreen.visible) {
            pauseScreen.HandleMouse(state);
        } else if (networkMap.visible) {
            networkMap.HandleMouse(state);
        } else if (dialog != null) {
            dialog.HandleMouse(state);
        } else if (powerWidget.visible
            && powerWidget.blockMouse) {
            powerWidget.HandleMouse(state);
        } else if (state.on) {
            if(sleepMouse) {
				sleepMouse = mouse.nowPos == mouse.prevPos;
            }
            //bool moved = mouseScreenPos != state.SurfaceCellPosition;
            //var mouseScreenPos = state.SurfaceCellPosition;
            var mouseScreenPos = new XY(state.pos.x / 8, state.pos.y / 8) + (0, 1);
            
            //(var a, var b) = (state.SurfaceCellPosition, state.SurfacePixelPosition);
            //Placeholder for mouse wheel-based weapon selection
            if (mouse.deltaWheel > 0) {
                if (playerControls.input.Shift) {
                    playerShip.NextSecondary();
                } else {
                    playerShip.NextPrimary();
                }
                playerControls.input.UsingMouse = true;
            } else if (mouse.deltaWheel < 0) {
                if (playerControls.input.Shift) {
                    playerShip.PrevSecondary();
                } else {
                    playerShip.PrevPrimary();
                }

                playerControls.input.UsingMouse = true;
            }

            var centerOffset = new XY(mouseScreenPos.x, sf.GridHeight - mouseScreenPos.y) - new XY(sf.GridWidth / 2, sf.GridHeight / 2);
            centerOffset *= uiMegamap.viewScale;
            mouseWorldPos = (centerOffset.Rotate(camera.rotation) + camera.position);
            if (mouse.middle == Pressing.Pressed && !playerControls.input.Shift) {
                TargetMouse();
            } else if(state.middleDown && playerControls.input.Shift) {
                playerControls.input.FirePrimary = true;
                playerControls.input.FireSecondary = true;
                playerControls.input.UsingMouse = true;
            }
            bool enableMouseTurn = !sleepMouse;
            //Update the crosshair if we're aiming with it
            if (playerShip.GetTarget(out var t) && t == crosshair) {
                crosshair.position = mouseWorldPos;
                crosshair.velocity = playerShip.velocity;
                //If we set velocity to match player's velocity, then the weapon will aim directly at the crosshair
                //If we set the velocity to zero, then the weapon will aim to the lead angle of the crosshair
                //crosshair.Update();
                //Idea: Aiming with crosshair disables mouse turning
                enableMouseTurn = false;
            }
            //Also enable mouse turn with Power Menu
            if (enableMouseTurn && playerShip.ship.rotating == Rotating.None) {
                var playerOffset = mouseWorldPos - playerShip.position;
                if (playerOffset.xi != 0 || playerOffset.yi != 0) {
                    var radius = playerOffset.magnitude;
                    var facing = XY.Polar(playerShip.rotationRad, radius);
                    var aim = playerShip.position + facing;
                    var off = (mouseWorldPos - aim).magnitude;
                    var tolerance = Sqrt(radius) / 3;
                    uint c = off < tolerance ? ABGR.White : ABGR.SetA(ABGR.White, 255 * 3 / 5);
                    EffectParticle.DrawArrow(world, mouseWorldPos, playerOffset, c);
                    //EffectParticle.DrawArrow(World, aim, facing, Color.Yellow);
                    var mouseRads = playerOffset.angleRad;
                    playerShip.SetRotatingToFace(mouseRads);
                    playerControls.input.TurnRight = playerShip.ship.rotating == Rotating.CW;
                    playerControls.input.TurnLeft = playerShip.ship.rotating == Rotating.CCW;
                    playerControls.input.UsingMouse = true;
                }
            }
            if (state.leftDown) {
                if (playerControls.input.Shift) {
                    playerControls.input.FireSecondary = true;
                } else {
                    playerControls.input.FirePrimary = true;
                }
                playerControls.input.UsingMouse = true;
            }
            if (state.rightDown) {
                playerControls.input.Thrust = true;
                playerControls.input.UsingMouse = true;
            }
        }
    }
	public void TargetMouse () {
		var targetList = new List<ActiveObject>(
					world.entities.all
					.OfType<ActiveObject>()
					.OrderBy(e => (e.position - mouseWorldPos).magnitude)
					.Distinct()
					);
		//Set target to object closest to mouse cursor
		//If there is no target closer to the cursor than the playership, then we toggle aiming by crosshair
		//Using the crosshair, we can effectively force any omnidirectional weapons to point at the crosshair
		if(targetList.First() == playerShip) {
			if(playerShip.GetTarget(out var t) && t == crosshair) {
				crosshair.active = false;
				playerShip.ClearTarget();
			} else {
				crosshair.active = true;
				playerShip.SetTargetList([crosshair]);
			}
		} else {
			playerShip.targetList = targetList;
			playerShip.targetIndex = 0;
			playerShip.UpdateWeaponTargets();
		}
	}
}
public class Noisemaker : Ob<EntityAdded>, IDestroyedListener, IDamagedListener, IWeaponListener, Ob<Projectile.Detonated> {
    public Action<SoundCtx> PlaySound { get; set; }
    private class AutoLoad : Attribute {}
    [AutoLoad]
    public static readonly byte[]
        generic_fire,
        generic_explosion,
        generic_exhaust,
        generic_damage,
        generic_shield_damage,
        target_set,
        target_clear,
        generic_missile,
        autopilot_on,
        autopilot_off,
        dock_start,
        dock_end,
        power_charge, power_release;
    public static byte[] Load(string file) => File.ReadAllBytes($"{Assets.ROOT}/sounds/{file}.wav");
    static Noisemaker() {
        var props = typeof(Noisemaker)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Where(p => p.GetCustomAttributes(true).OfType<AutoLoad>().Any()).ToList();
        foreach (var p in props) {
            p.SetValue(null, Load(p.Name));
        }
    }
    readonly PlayerShip player;
    readonly List<IShip> exhaustList = [];
    const float distScale = 1 / 16f;
    public readonly SoundCtx button_press = new(File.ReadAllBytes($"{Assets.ROOT}/sounds/button_press.wav"), 33);
    private readonly ListTracker<SoundCtx>
        _exhaust = new([..(0..16).Select(i => new SoundCtx([], 10))]),
        _gunfire = new([.. (0..8).Select(i => new SoundCtx([], 50))]),
        _damage = new([.. (0..8).Select(i => new SoundCtx([], 50))]),
        _explosion = new([.. (0..4).Select(i => new SoundCtx([], 75))]),
        _discovery = new([.. (0..8).Select(i => new SoundCtx([], 25))]);
    private class Vol : Attribute {}
    [Vol]
    public SoundCtx targeting, autopilot, dock, powerCharge;
    private Dictionary<SoundCtx, float> regular_volumes = new();
    public Noisemaker(PlayerShip player) {

        var props = typeof(Noisemaker)
            .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Where(p => p.GetCustomAttributes(true).OfType<Vol>().Any()).ToList();
        foreach (var p in props) {
            p.SetValue(this, new SoundCtx([], 50));
        }
        var sounds = new[] { _exhaust.list, _gunfire.list, _damage.list, _explosion.list, _discovery.list }.SelectMany(l => l)
            //.Concat(new[] { targeting, autopilot, dock, powerCharge })
            ;
        foreach(var s in sounds) {
            regular_volumes[s] = s.volume;
        }
        this.player = player;

        player.onTargetChanged += new Container<TargetChanged>(pl => {
            targeting.data = pl.targetIndex == -1 ? target_clear : target_set;
            PlaySound?.Invoke(targeting);
        });
    }
    public void PlayDiscoverySound(SoundCtx sb) {
        if(_discovery.list.Any(s => s.data == sb.data)) {
            return;
        }
        PlayWorldSound(GetNextChannel(_discovery), sb.data);
    }
    public void PlayPowerCharge() {
        powerCharge.data = power_charge;
        PlaySound(powerCharge);
    }
    public void PlayPowerRelease() {
        powerCharge.data = power_release;
        PlaySound(powerCharge);
    }
    public void PlayAutopilot(bool active) {
        autopilot.data = (active ? autopilot_on : autopilot_off);
        PlaySound?.Invoke(autopilot);
    }
    public void PlayError() {
        targeting.data = target_clear;
        PlaySound(targeting);
    }
    public void PlayDocking(bool start) {
        dock.data = (start ? dock_start : dock_end);
        PlaySound?.Invoke(dock);
    }
    double time;
    public void Update(double delta) {
        time += delta;
        if(time > 0.4) {
            time = 0;
            var s = player.world.entities.all.OfType<IShip>()
                .Where(s => s.thrusting)
                .OrderBy(sh => player.position.Dist(sh.position))
                .Zip(_exhaust.list);
            foreach((var ship, var sound) in exhaustList.Zip(_exhaust.list)) {
                sound.Stop?.Invoke();
            }
            exhaustList.Clear();
            foreach ((var ship, var sound) in s) {
                PlayWorldSound(sound, ship, generic_exhaust);
                exhaustList.Add(ship);
            }
        } else {
            foreach((var ship, var sound) in exhaustList.Zip(_exhaust.list)) {
                sound.pos = player.position.To(ship.position).Scale(distScale);
            }
        }
    }
    public void Register(Universe u) {
        var obj = u.GetAllEntities().OfType<Entity>();
        foreach (var a in obj) {
            Register(a);
        }
        u.onEntityAdded += this;
    }
    private void Register(Entity e) {
        if (e is ActiveObject a) {
            ((IDestroyedListener)this).Register(a);
            ((IDamagedListener)this).Register(a);
            ((IWeaponListener)this).Register(a);
        }
    }
    public SoundCtx GetNextChannel(ListTracker<SoundCtx> i) => i.GetFirstOrNext(s => !s.playing);
    public void Observe(EntityAdded ev) {
        Register(ev.e);
    }
    public void Observe(IDestroyedListener.Destroyed ev) {
        var e = ev.destroyed;
        if(e.world != player.world) {
            return;
        }
        PlayWorldSound(_explosion, e, generic_explosion);
    }
    public void Observe(IDamagedListener.Damaged ev) {
        var (e, p) = ev;
        if (e.world != player.world) {
            return;
        }
        PlayWorldSound(_damage, e, p.hitHull ? generic_damage : generic_shield_damage);
    }
    public void Observe(IWeaponListener.WeaponFired ev) {
        var (e, w, pr, sound) = ev;
        if (e.world != player.world) {
            return;
        }
        foreach (var p in pr) {
            p.onDetonated += this;
        }
        if (sound) {
            PlayWorldSound(_gunfire, e, (w.desc?.sound ?? generic_fire));
        }
    }
    public void Observe(Projectile.Detonated d) {
        var sb = d.source.desc.detonateSound;
        if (sb == null) {
            return;
        }
        PlayWorldSound(_gunfire,
            d.source,
            sb);
    }
    private void PlayWorldSound(ListTracker<SoundCtx> s, Entity e, byte[] data) =>
        PlayWorldSound(GetNextChannel(s), e, data);
    private void PlayWorldSound(SoundCtx s, Entity e, byte[] data) {
        s.pos = player.position.To(e.position).Scale(distScale);
        PlayWorldSound(s, data);
    }
    private void PlayWorldSound(SoundCtx s, byte[] sb) {
        s.data = sb;
        s.volume = regular_volumes[s] * (float)Max(0, 1 - player.ship.silence);
        PlaySound?.Invoke(s);
    }
}
public class BackdropConsole {
    public Action<Sf> Draw { set; get; }
    public int Width => sf.GridWidth;
    public int Height => sf.GridHeight;
    public Camera camera;
    private readonly XY screenCenter;
    private Backdrop backdrop;
    public Sf sf;
	public BackdropConsole(Viewport view) {
        sf = new Sf(view.Width, view.Height, Fonts.FONT_8x8);
		this.camera = view.camera;
        this.backdrop = view.world.backdrop;
        screenCenter = new(Width / 2f, Height / 2f);
    }
    public BackdropConsole(Monitor m) {
        this.sf = new Sf(m.Width, m.Height, Fonts.FONT_8x8);
        this.camera = m.camera;
        this.backdrop = m.world.backdrop;
        screenCenter = new XY(Width / 2f, Height / 2f);
    }
    public void Update(TimeSpan delta) {}
    public void Render(TimeSpan drawTime) {
        sf.Clear();
        for (int x = 0; x < Width; x++) {
            for (int y = 0; y < Height; y++) {
                //var g = this.GetGlyph(x, y);
                var offset = new XY(x, Height - y) - screenCenter;
                var location = camera.position + offset.Rotate(camera.rotation);
                sf.Tile[x, y] = backdrop.GetTile(location, camera.position);
            }
        }
        Draw?.Invoke(sf);
    }
    public Tile GetTile(int x, int y) {
        var offset = new XY(x, Height - y) - screenCenter;
        var location = camera.position + offset.Rotate(camera.rotation);
        return backdrop.GetTile(location, camera.position);
    }
}
public class Megamap {
	public Action<Sf> Draw { set; get; }
	int Width => sf.GridWidth;
    int Height => sf.GridHeight;
    Camera camera;
    PlayerShip player;
    GeneratedLayer background;
    public double targetViewScale = 1, viewScale = 1;
    double time;
    Dictionary<(int, int), List<(Entity entity, double distance)?>> scaledEntities;
    XY screenSize, screenCenter;
    public byte alpha;
    public Sf sf;
    public Megamap(Monitor m, GeneratedLayer back) {
        this.sf = new Sf(m.Width, m.Height, Fonts.FONT_8x8);
        this.camera = m.camera;
        this.player = m.playerShip;
        this.background = back;

        screenSize = new(Width, Height);
        screenCenter = screenSize / 2;
    }
    public double delta => Min(targetViewScale / (2 * 30), 1);
    public void HandleKey(KB kb) {
        var p = kb.IsPress;
        var d = kb.IsDown;
        if (d(KC.LeftControl) || d(KC.RightControl)) {
            if (p(KC.OemMinus)) {
                targetViewScale *= 2;
            }
            if (p(KC.OemPlus)) {
                targetViewScale /= 2;
                if (targetViewScale < 1) {
                    targetViewScale = 1;
                }
            }
            if (p(KC.D0)) {
                targetViewScale = 1;
            }
        } else {
            if (d(KC.OemMinus)) {
                viewScale += delta;
                targetViewScale = viewScale;
            }
            if (d(KC.OemPlus)) {
                viewScale -= delta;
                if (viewScale < 1) {
                    viewScale = 1;
                }
                targetViewScale = viewScale;
            }
        }
        if (p(KC.M)) {
            if(targetViewScale >= 16) {
                targetViewScale = 1.0;
            } else {
                targetViewScale = Floor(targetViewScale)*4;
            }
        }
    }
    public void Update(TimeSpan delta) {
        var d = targetViewScale - viewScale;
        /*
        if(Math.Abs(d) < 0.1) {
            viewScale += d;
        } else {
            viewScale += d / 10;
        }
        */
        viewScale += MaxMagnitude(MinMagnitude(d, Sign(d)*0.1), d / 10);
        alpha = (byte)(255 * Min(1, viewScale - 1));
        time += delta.TotalSeconds;
#nullable enable
        scaledEntities = player.world.entities.TransformSelectList<(Entity entity, double distance)?>(
                e => (screenCenter + ((e.position - player.position) / viewScale).Rotate(-camera.rotation)).flipY + (0, Height),
                ((int x, int y) p) => p is (> -1, > -1) && p.x < Width && p.y < Height,
                ent => ent is { tile: not null } and not ISegment && player.GetVisibleDistanceLeft(ent) is var dist and > 0 ? (ent, dist) : null
            );
    }
    public void Render(TimeSpan delta) {
        sf.Clear();
        var alpha = this.alpha;
        if (alpha > 0) {
            if(alpha < 128) {
                alpha = (byte)(128 * Sqrt(alpha / 128f));
            }
            for (int x = 0; x < Width; x++) {
                for (int y = 0; y < Height; y++) {
                    var offset = new XY((x - screenCenter.x) * viewScale, (y - screenCenter.y) * viewScale).Rotate(camera.rotation);
                    var pos = player.position + offset;
                    void Render(Tile t) {
                        var glyph = t.Glyph;
                        var background = ABGR.PremultiplySet(t.Background, alpha);
                        var foreground = ABGR.PremultiplySet(t.Foreground, alpha);
                        sf.SetTile(x, Height - y - 1, new Tile(foreground, background, glyph));
                    }
                    var environment = player.world.backdrop.planets.GetTile(pos.Snap(viewScale), XY.Zero);
                    if (environment.IsVisible) {
                        Render(environment);
                        continue;
                    }
                    /*
                    environment = player.world.backdrop.orbits.GetTile(pos.Snap(viewScale), XY.Zero);
                    if (IsVisible(environment)) {
                        Render(environment);
                        continue;
                    }
                    */
                    environment = player.world.backdrop.nebulae.GetTile(pos.Snap(viewScale), XY.Zero);
                    if (environment.IsVisible) {
                        Render(environment);
                        continue;
                    }
                    var starlight = ABGR.PremultiplySet(player.world.backdrop.starlight.GetBackgroundFixed(pos), 255);
                    string str = starlight.ToString("X");
                    if(starlight != 0xFF000000) {
                        int i = 0;
                    }

                    var cg = this.background.GetTileFixed(new XY(x, y));
                    //Make sure to clone this so that we don't apply alpha changes to the original
                    var glyph = cg.Glyph;
                    var background = ABGR.BlendPremultiply(cg.Background, starlight, alpha);
                    var foreground = ABGR.PremultiplySet(cg.Foreground, alpha);
                    sf.SetTile(x, Height - y - 1, new Tile(foreground, background, glyph));
                }
            }
            
#if false
            var visiblePerimeter = new Rect(Width / 2 - w/2, Height/2 - h/2, w, h);
            foreach (var (x,y) in visiblePerimeter.Perimeter) {
                var b = sf.Back[x, y];
                sf.Back[x, y] = ABGR.BlendPremultiply(b, ABGR.RGBA(255, 255, 255, (byte)(128/viewScale)));
            }
#else

            if(viewScale > 1) {
				int w = (int)(Width / (viewScale) - 1);
				int h = (int)(Height / (viewScale) - 1);
				Sf.DrawRect(sf, Width / 2 - w / 2, Height / 2 - h / 2, w, h, new() {
                    b = ABGR.Transparent,
                });
            }
#endif            
            foreach ((var offset, var visible) in scaledEntities) {
                var (x, y) = offset;
                (var entity, var distance) = visible[(int)time % visible.Count].Value;
                var t = entity.tile;
                var f = t.Foreground;
                //Apply stealth
                const double threshold = 16d;
                if (distance < threshold) {
                    f = ABGR.MA(f) * 0 + (byte)(255 * distance / threshold);
                }
                t = new(f, ABGR.Blend(sf.Back[x, y], t.Background), t.Glyph);
                sf.Tile[x, y] = t;
            }
            /*
            Parallel.ForEach(scaledEntities.space, pair => {
                (var offset, var ent) = pair;
            });
            */
            /*
            var scaledEffects = player.world.effects.space.DownsampleSet(viewScale);
            foreach ((var p, HashSet<Effect> set) in scaledEffects.space) {
                var visible = set.Where(t => !(t is ISegment)).Where(t => t.tile != null);
                if (visible.Any()) {
                    var e = visible.ElementAt((int)time % visible.Count());
                    var offset = (e.position - player.position) / viewScale;
                    var (x, y) = screenCenter + offset.Rotate(-camera.rotation);
                    y = Height - y;
                    if (x > -1 && x < Width && y > -1 && y < Height) {
                        if (rendered.Contains((x, y))) {
                            continue;
                        }

                        var t = new ColoredGlyph(e.tile.Foreground, this.GetBackground(x, y), e.tile.Glyph);
                        this.SetCellAppearance(x, y, t);
                    }
                }
            }
            */
        }
        Draw?.Invoke(sf);
    }
}
public class Vignette : Ob<PlayerShip.Damaged>, Ob<PlayerShip.Destroyed> {
	public Action<Sf> Draw { set; get; }
	public Sf sf;
    public int Width => sf.GridWidth;
    public int Height => sf.GridHeight;
    PlayerShip player;
    public float glowAlpha;
    public bool armorDecay;
    public HashSet<EffectParticle> particles;
    public XY screenCenter;
    public int ticks;
    public Random r;
    public int[,] grid;
    public bool chargingUp;
    
    public int lightningHit;
    public int flash;
	private int recoveryTime;
	private uint glowColor = PowerType.glowColors[Voice.Orator];
	private double silence;
	private double[,] silenceGrid;
	private Viewport silenceViewport;

    public void Observe(PlayerShip.Damaged ev) {
        var (pl, pr) = ev;
        if (pr.desc.lightning) {
            lightningHit = 5;
        }
        if (pr.desc.blind?.Roll() is int db) {
            flash = Max(db, flash);
        }
    }
    public void Observe(PlayerShip.Destroyed ev) {
        
    }
    public Vignette(Mainframe main) {
        player = main.playerShip;
        sf = new Sf(main.Width, main.Height, Fonts.FONT_8x8);
        player.onDamaged += this;
        player.onDestroyed += this;
        glowAlpha = 0;
        particles = [];
        screenCenter = new(Width / 2, Height / 2);
        r = new();
        grid = new int[Width, Height];
        silenceGrid = new double[Width, Height];
        for (int x = 0; x < Width; x++) {
            for (int y = 0; y < Height; y++) {
                grid[x, y] = r.Next(0, 240);
                silenceGrid[x, y] = r.NextDouble();
            }
        }
        silenceViewport = new Viewport(Width, Height, main.monitor with { world = main.silenceSystem });
    }
    public void Update(TimeSpan delta) {
        silenceViewport.Update(delta);
        armorDecay = false && player.hull is LayeredArmor la && la.layers.Any(a => a.corrode.Any());
        var charging = player.powers.Where(p => p.charging);
        if (charging.Any()) {
            var (power, charge) = charging.Select(p => (power:p, charge: p.invokeCharge / p.invokeDelay)).MaxBy(p => p.charge);
            if (glowAlpha < charge) {
                glowAlpha += (float)((charge - glowAlpha) / 10f);
            }
            if(power != null) {
                glowColor = ABGR.Blend(glowColor, ABGR.SetA(power.type.glowColor, (byte)(255 * delta.TotalSeconds)));
            }
            if (recoveryTime < 360) {
                recoveryTime++;
            }
            chargingUp = true;
        } else {
            if (player.CheckGate(out Stargate gate)) {
                var targetAlpha = 1f;
                if (glowAlpha < targetAlpha) {
                    glowAlpha += (targetAlpha - glowAlpha) / 30;
                }
                glowColor = ABGR.Blend(glowColor, ABGR.SetA(ABGR.White, (byte)(255 * delta.TotalSeconds)));
            } else {
                chargingUp = false;
                glowAlpha -= (float)(glowAlpha * delta.TotalSeconds);
                if (glowAlpha < 0.01 && recoveryTime > 0) {
                    recoveryTime -= (int)(60 * delta.TotalSeconds);
                }
            }
        }
        ticks++;
        if (ticks % 5 == 0 && player.ship.disruption?.ticksLeft > 30) {
            var i = 0;
            var screenPerimeter = new Rect(i, i, Width - i * 2, Height - i * 2);
            foreach (var p in screenPerimeter.Perimeter.Select(p => new XY(p))) {
                if (r.Next(0, 10) == 0) {
                    var speed = 15;
                    var lifetime = 60;
                    var v = new XY(p.xi == 0 ? speed : p.xi == screenPerimeter.width - 1 ? -speed : 0,
                                    p.yi == 0 ? speed : p.yi == screenPerimeter.height - 1 ? -speed : 0);
                    particles.Add(new EffectParticle(p, (ABGR.Cyan, ABGR.Transparent, '#'), lifetime) { Velocity = v });
                }
            }
        }
        Parallel.ForEach(particles, p => {
            p.position += p.Velocity / Constants.TICKS_PER_SECOND;
            p.lifetime--;
            p.Velocity -= p.Velocity / 15;

            p.tile = p.tile with { Foreground = ABGR.SetA(p.tile.Foreground, (byte)(255 * Min(p.lifetime / 30f, 1))) };
        });
        particles.RemoveWhere(p => !p.active);
        lightningHit--;
        flash--;
    }
    public void Render(TimeSpan delta) {
        sf.Clear();

        if (!player.active) {
            return;
        }
        //XY screenSize = new XY(Width, Height);
        //Set the color of the vignette
        var borderColor = ABGR.Black;
        var borderSize = 6d;

        if (glowAlpha > 0) {
            var v = ABGR.SetA(glowColor, (byte)(255 * (float)Min(1, glowAlpha * 1.5)));
            borderColor = ABGR.Premultiply(ABGR.Blend(borderColor, v));
            borderSize += 12 * glowAlpha;
        }

		if(player.mortalTime > 0) {
			borderColor = ABGR.Blend(borderColor, ABGR.SetA(ABGR.Red, (byte)(Min(1, player.mortalTime / 4.5) * 255)));
			var fraction = player.mortalTime - Truncate(player.mortalTime);
			borderSize += 6 * fraction;
		}
		
        if (flash > 0) {
            borderSize += Min(8, 8 * flash / 30f);
            borderColor = ABGR.Blend(borderColor, ABGR.SetA(ABGR.Gray,(byte)Min(255, 255 * flash / 30)));
        } else if (player.ship.disruption?.ticksLeft > 0) {
            var ticks = player.ship.disruption.ticksLeft;
            var strength = Min(ticks / 60f, 1);
            borderSize += 5 * strength;
            borderColor = ABGR.Blend(borderColor, ABGR.SetA(ABGR.Cyan, (byte)(128 * strength)));
        } else {
            var b = player.world.backdrop.starlight.GetBackgroundFixed(player.position);
            var br = 255 * ABGR.GetLightness(b);
            borderSize += (0 * 5f * Pow(br / 255, 1.4));
            borderColor = ABGR.Blend(borderColor, ABGR.SetA(b,(byte)(br)));
        }
        borderSize = Clamp(borderSize, 0, 16);
        //Cover the border in visual snow
        if (player.ship.blindTicks > 0) {
            for (int i = 0; i < borderSize; i++) {
                var d = Pow(1d * i / borderSize, 1.4);
                var alpha = (byte)(255 - d * 255 / 2);
                var screenPerimeter = new Rect(i, i, Width - i * 2, Height - i * 2);
                foreach (var (x,y) in screenPerimeter.Perimeter) {
                    //var back = this.GetBackground(point.X, point.Y).Premultiply();
                    var inc = (byte)r.Next(102);
                    sf.Back[x, y] = ABGR.SetA(ABGR.ToGray(ABGR.IncRGB(borderColor, inc)), alpha);
                }
            }
        } else {
            for (int i = 0; i < borderSize; i++) {
                var d = Pow(1d * i / borderSize, 1.4);
                var alpha = (byte)(255 - 255 * d);
                var c = ABGR.SetA(borderColor, alpha);
                var screenPerimeter = new Rect(i, i, Width - i * 2, Height - i * 2);
                foreach (var (x,y) in screenPerimeter.Perimeter) {
                    //var back = this.GetBackground(point.X, point.Y).Premultiply();
                    sf.Back[x, y] = c;
                }
            }
        }
        if (lightningHit > 0) {
            var i = 1;
            var c = ABGR.RGBA(255, 0, 0, (byte)(153 * lightningHit/5));
            foreach(var (x,y) in new Rect(i, i, Width - i * 2, Height - i * 2).Perimeter) {
                sf.Back[x, y] = ABGR.Blend(sf.Back[x, y], c);
                //Surface.SetBackground(x, y, c);
            }
        }
        if (armorDecay) {
            var i = 2;
            var c = ABGR.RGBA(255, 255, 0, (byte)(255 - 48 + (int)(Sin(ticks / 5f) * 24)));
            foreach (var (x,y) in new Rect(i, i, Width - i * 2, Height - i * 2).Perimeter) {
                sf.Back[x, y] = c;
            }
        }
        foreach (var p in particles) {
            var (x, y) = p.position;
            var (fore, glyph) = (p.tile.Foreground, p.tile.Glyph);
            sf.Tile[x, y] = new(fore, sf.Back[x, y], glyph);
        }
        silence += (player.ship.silence - silence) * delta.TotalSeconds * 10;
        if(silence > 0) {
            for(var x = 0; x < Width; x++) {
                for(var y = 0; y < Height; y++) {
                    var alpha = (byte)(255 * Min(1, silence / silenceGrid[x, y]));
                    if(alpha < 2) {
                        continue;
                    }
                    var t = silenceViewport.GetTile(x, y);
                    sf.Tile[x, Height - y - 1] = new Tile(ABGR.SetA(t.Foreground,alpha), ABGR.SetA(t.Background,alpha), (int)t.Glyph);
                }
            }
        }
        Draw?.Invoke(sf);
    }
}
public class Readout {
	public Action<Sf> Draw { set; get; }
	/*
    struct Snow {
        public char c;
        public double factor;
    }
    Snow[,] snow;
    */
	public bool visible = true;
    Camera camera;
    PlayerShip player;
    public double viewScale;

    public int arrowDistance;
    public int Width => sf_ui.GridWidth;
    public int Height => sf_ui.GridHeight;
    XY screenSize => new XY(Width, Height);
    XY screenCenter => screenSize / 2;
    public Sf sf_ui;
    public Readout(Monitor m) {
        camera = m.camera;
        player = m.playerShip;
		sf_ui = new Sf(m.Width * 4/3, m.Height, Fonts.FONT_6x8);
		//arrowDistance = Math.Min(Width, Height)/2 - 6;
		arrowDistance = 24;
        /*
        char[] particles = {
            '%', '&', '?', '~'
        };
        snow = new Snow[width, height];
        for(int x = 0; x < width; x++) {
            for(int y = 0; y < height; y++) {
                snow[x, y] = new Snow() {
                    c = particles.GetRandom(r),
                    factor = r.NextDouble()
                };
            }
        }
        */
    }
    public void Update(TimeSpan delta) {
        if (player.GetTarget(out var playerTarget)) {
            DrawTargetArrow(playerTarget, ABGR.Yellow);
        }
        foreach (var t in player.tracking.Keys) {
            DrawTargetArrow(t, ABGR.SpringGreen);
        }
        //var autoTarget = player.devices.Weapons.Select(w => w.target).FirstOrDefault();
        var autoTargets = player.primary.item?.targeting?.GetMultiTarget()
            .Except([ null ])
            .Except(player.tracking.Keys)
            .Take(player.primary.item.desc.burstSize);
        foreach (var at in autoTargets ?? []) {
            DrawTargetArrow(at, ABGR.LightYellow);
        }
        void DrawTargetArrow(ActiveObject target, uint c) {
            var o = (target.position - player.position);
            var offset = o / viewScale;
            if (Abs(offset.x) > Width / 2 - 6 || Abs(offset.y) > Height / 2 - 6) {
                var offsetNormal = offset.normal.flipY;
                //var p = screenCenter + offsetNormal * arrowDistance;
                var smallScreen = screenSize - (20, 20);
                var smallCenter = smallScreen / 2;
                var p = Main.GetBoundaryPoint(smallScreen, offsetNormal.angleRad);
                var centerOffset = (p - smallCenter).flipY;

                var loc = player.position + centerOffset;
                EffectParticle.DrawArrow(player.world, loc, offset, c);
            }
            if(target is Station st) {
                Heading.Box(st, c);
            } else {
                Heading.Crosshair(target.world, target.position, c);
            }
        }
    }
    public void Render(TimeSpan drawTime) {
        sf_ui.Clear();
        var wd = 42;
		var messageY = Height * 3 / 5;
        int targetX = wd + 4, targetY = 1;
        int tick = player.world.tick;
        for (int i = 0; i < player.messages.Count; i++) {
            var message = player.messages[i];
            var line = message.Draw();
            var xStart = 52;
            var x = xStart - line.Count();
            sf_ui.Print(x, messageY, line);
            if (message is Transmission t) {
                //Draw a line from message to source

                var screenCenterOffset = ((xStart, Height - messageY) - screenCenter) * (1, 1);
                var messagePos = (player.position + screenCenterOffset).roundDown;
                var sourcePos = t.source.position.roundDown;
                sourcePos = player.position + (sourcePos - player.position).Rotate(-camera.rotation) / viewScale;
                if (messagePos.yi == sourcePos.yi) {
                    continue;
                }
                var screenX = xStart;
                var screenY = messageY;
                var (f, b) = line.Any() ? (line[0].Foreground, ABGR.Transparent) : (ABGR.White, ABGR.Transparent);
                var lineWidth = Line.Single;

                screenX++;
                messagePos.x++;
                sf_ui.Tile[screenX, screenY] = new Tile(f, b, BoxInfo.IBMCGA.glyphFromInfo[new BoxGlyph {
                    e = Line.Single,
                    //n = Line.Single,
                    //s = Line.Single
                    w = Line.Single
                }]);
                screenX++;
                messagePos.x++;


                for (int j = 0; j < i; j++) {
					sf_ui.Tile[screenX, screenY] = new Tile(f, b, BoxInfo.IBMCGA.glyphFromInfo[new BoxGlyph {
                        e = lineWidth,
                        w = lineWidth
                    }]);
                    screenX++;
                    messagePos.x++;
                }
                /*
                var offset = sourcePos - messagePos;
                int screenLineY = Math.Max(-(Height - screenY - 2), Math.Min(screenY - 2, offset.yi < 0 ? offset.yi - 1 : offset.yi));
                int screenLineX = Math.Max(-(screenX - 2), Math.Min(Width - screenX - 2, offset.xi));
                */
                var offset = (sourcePos - player.position) * (4/3f, 1) + (0, 0);
                var offsetLeft = new XY(0, 0);
                var truncateX = Abs(offset.x) > Width / 2 - 3;
                var truncateY = Abs(offset.y) > Height / 2 - 3;
                if (truncateX || truncateY) {
                    var sourcePosEdge = Main.GetBoundaryPoint(screenSize, offset.angleRad) - screenSize / 2 + player.position;
                    offset = sourcePosEdge - player.position;
                    if (truncateX) { offset.x -= Sign(offset.x) * (i + 2); }
                    if (truncateY) { offset.y -= Sign(offset.y) * (i + 2); }
                    offsetLeft = sourcePos - sourcePosEdge;
                }
                offset += player.position - messagePos;
                var screenLineY = offset.yi + (offset.yi < 0 ? 0 : 1);
                var screenLineX = offset.xi;
                if (screenLineY != 0) {
                    sf_ui.Tile[screenX, screenY] = new Tile(f, b, BoxInfo.IBMCGA.glyphFromInfo[new BoxGlyph {
                        n = offset.y > 0 ? lineWidth : Line.None,
                        s = offset.y < 0 ? lineWidth : Line.None,
                        w = lineWidth,
                        e = offset.y == 0 ? lineWidth : Line.None
                    }]);
                    screenY -= Sign(screenLineY);
                    screenLineY -= Sign(screenLineY);

                    while (screenLineY != 0) {
						sf_ui.Tile[screenX, screenY] = new Tile(f, b, BoxInfo.IBMCGA.glyphFromInfo[new BoxGlyph {
                            n = lineWidth,
                            s = lineWidth
                        }]);
                        screenY -= Sign(screenLineY);
                        screenLineY -= Sign(screenLineY);
                    }
                }
                if (screenLineX != 0) {
					sf_ui.Tile[screenX, screenY] = new Tile(f, b, BoxInfo.IBMCGA.glyphFromInfo[new BoxGlyph {
                        n = offset.y < 0 ? lineWidth : offset.y > 0 ? Line.None : Line.Single,
                        s = offset.y > 0 ? lineWidth : offset.y < 0 ? Line.None : Line.Single,

                        e = offset.x > 0 ? lineWidth : offset.x < 0 ? Line.None : Line.Single,
                        w = offset.x < 0 ? lineWidth : offset.x > 0 ? Line.None : Line.Single
                    }]);
                    screenX += Sign(screenLineX);
                    screenLineX -= Sign(screenLineX);

                    while (screenLineX != 0) {
						sf_ui.Tile[screenX, screenY] = new Tile(f, b, BoxInfo.IBMCGA.glyphFromInfo[new BoxGlyph {
                            e = lineWidth,
                            w = lineWidth
                        }]);
                        screenX += Sign(screenLineX);
                        screenLineX -= Sign(screenLineX);
                    }
                }
                /*
                screenX += Math.Sign(offsetLeft.x);
                screenY -= Math.Sign(offsetLeft.y);
                this.SetCellAppearance(screenX, screenY, new ColoredGlyph('*', f, b));
                */
            }
            messageY++;
        }
        const int BAR = 8;


        var target = default(ActiveObject);
        if (player.GetTarget(out target)) {
            sf_ui.Print(targetX, targetY++, Tile.Arr("[Target]", ABGR.White, ABGR.Black));
        } else if(player.primary.item?.target is ActiveObject found) {
            target = found;
            sf_ui.Print(targetX, targetY++, Tile.Arr("[Auto]", ABGR.White, ABGR.Black));
        } else {
            goto SkipTarget;
        }
        sf_ui.Print(targetX, targetY++, Tile.Arr(target.name, player.tracking.ContainsKey(target) ? ABGR.SpringGreen : ABGR.White, ABGR.Black));
        PrintTarget(targetX, targetY, target);
    SkipTarget:
        
        void PrintTarget(int x, int y, ActiveObject target) {
            var b = ABGR.Black;
            switch (target) {
                case AIShip ai:
                    Print(ai.devices);
                    PrintHull(ai.hull);
                    break;
                case Station s:
                    PrintHull(s.damageSystem);
                    break;
            }
            void Print(Circuit devices) {
                var solars = devices.Solar;
                var reactors = devices.Reactor;
                var weapons = devices.Weapon;
                var shields = devices.Shield;
                var misc = devices.Installed.OfType<Service>();
                foreach (var reactor in reactors) {
                    var bar = default(Tile[]);
                    if (reactor.energy > 0) {
                        var(arrow, f) = reactor.energyDelta switch {
                            < 0 => ('<', ABGR.Yellow),
                            > 0 => ('>', ABGR.Cyan),
                            _   => ('=', ABGR.White)
                        };
                        int length = (int)Ceiling(BAR * reactor.energy / reactor.desc.capacity);
                        bar = [..Tile.Arr($"{new string('=', length - 1)}{arrow}", f, b),
                            ..Tile.Arr(new('=', BAR - length), ABGR.Gray, b)];
                    } else {
                        bar = Tile.Arr(new('=', BAR), ABGR.Gray, b);
                    }

                    var l = (int)Ceiling(-BAR * (double)reactor.energyDelta / reactor.maxOutput);
                    Array.Copy(bar[..l].Select(t => t with { Background = ABGR.DarkKhaki }).ToArray(), bar, l);

                    sf_ui.Print(x, y, [
                        ..Tile.Arr("[", ABGR.White, b),
                        ..bar,
                        ..Tile.Arr("]", ABGR.White, b),
                        (ABGR.White, b, ' '),
                        ..Tile.Arr($"{reactor.source.type.name}", ABGR.White, b)
                        ]);
                    y++;
                }
                if (solars.Any()) {
                    foreach (var s in solars) {
                        int length = (int)Ceiling(BAR * (double)s.maxOutput / s.desc.maxOutput);
                        int sublength = s.maxOutput > 0 ? (int)Ceiling(length * (-s.energyDelta) / s.maxOutput) : 0;
                        Tile[] bar = [
                            ..Tile.Arr(new('=', sublength), ABGR.Yellow, ABGR.DarkKhaki),
                            ..Tile.Arr(new('=', length - sublength), ABGR.Cyan, b),
                            ..Tile.Arr(new('=', BAR - length), ABGR.Gray, b)];
                        /*
                        int l = (int)Math.Ceiling(-16f * s.maxOutput / s.desc.maxOutput);
                        for (int i = 0; i < l; i++) {
                            bar[i].Background = Color.DarkKhaki;
                            bar[i].Foreground = Color.Yellow;
                        }
                        */
                        sf_ui.Print(x, y, [
                            ..Tile.Arr("[", ABGR.White, b),
                            ..bar,
                            ..Tile.Arr("]", ABGR.White, b),
                            ..Tile.Arr(" ", ABGR.White, b),
                            ..Tile.Arr($"{s.source.type.name}", ABGR.White, b)
                            ]);
                        y++;
                    }
                    y++;
                }
                if (weapons.Any()) {
                    int i = 0;
                    foreach (var w in weapons) {
                        string enhancement;
                        if (w.mod != FragmentMod.EMPTY) {
                            enhancement = "+";
                        } else {
                            enhancement = "";
                        }
                        uint foreground =
                            false ?
                                ABGR.Gray :
                            w.firing || w.delay > 0 ?
                                ABGR.Yellow :
                            ABGR.White;
                        sf_ui.Print(x, y,[
                            ..Tile.Arr("[", ABGR.White, b),
                            ..w.GetBar(BAR),
                            ..Tile.Arr($"] {w.source.type.name} {enhancement}", foreground, b)]);
                        y++;
                        i++;
                    }
                    y++;
                }
                if (misc.Any()) {
                    foreach (var m in misc) {
                        var tag = m.source.type.name;
                        var f = ABGR.White;
                        sf_ui.Print(x, y, Tile.Arr($"{tag}", f, b));
                        y++;
                    }
                    y++;
                }
                if (shields.Any()) {
                    foreach (var s in shields.Reverse<Shield>()) {
                        string name = s.source.type.name;
                        var f =
                            false ?
                                ABGR.Gray :
                            s.hp == 0 || s.delay > 0 ?
								ABGR.Yellow :
                            s.hp < s.desc.maxHP ?
								ABGR.Cyan :
							ABGR.White;
                        int l = BAR * s.hp / s.desc.maxHP;
                        sf_ui.Print(x, y, Tile.Arr("[", f, b));
                        sf_ui.Print(x + 1, y, Tile.Arr(new('=', BAR), ABGR.Gray, b));
                        sf_ui.Print(x + 1, y, Tile.Arr(new('=', l), f, b));
                        sf_ui.Print(x + 1 + BAR, y, Tile.Arr($"] {name}", f, b));
                        y++;
                    }
                    y++;
                }
            }
            void PrintHull(HullSystem hull) {
                switch (hull) {
                    case LayeredArmor las: {
                            foreach (var armor in las.layers.Reverse<Armor>()) {
                                var f =
                                    tick - armor.lastDamageTick < 15 ?
                                        ABGR.Yellow :
                                    armor.hp > 0 ?
                                        ABGR.White :
                                    armor.canAbsorb ?
                                        ABGR.Orange :
                                    ABGR.Gray;
                                var bb =
                                    armor.corrode.Any() ?
                                        ABGR.Blend(ABGR.Black, ABGR.SetA(ABGR.Red, 128)) :
                                    tick - armor.lastRegenTick < 15 ?
                                        ABGR.Blend(ABGR.Black, ABGR.SetA(ABGR.Cyan, 128)) :
                                    !armor.allowRecovery ?
                                        ABGR.Blend(ABGR.Black, ABGR.SetA(ABGR.Yellow, 128)) :
                                        b;
                                var available = BAR * Min(armor.maxHP, armor.desc.maxHP) / Max(1, armor.desc.maxHP);

                                var active = available * Min(armor.hp, armor.maxHP) / Max(1, armor.maxHP);
                                sf_ui.Print(x, y, [
                                    ..Tile.Arr("[", f, b),
									..Tile.Arr(new('=', active), f, b),
									..Tile.Arr(new('=', available - active), ABGR.Gray, b),
									..Tile.Arr(new(' ', BAR - available), ABGR.Gray, b),
									..Tile.Arr($"] {armor.hp,3}/{armor.maxHP,3} ", f, b),
									..Tile.Arr($"{armor.source.type.name}", f, bb)
                                ]);
                                y++;
                            }
                            break;
                        }
                    case HP hp: {
                            var f = ABGR.White;
                            sf_ui.Print(x, y, Tile.Arr("[", f, b));
                            sf_ui.Print(x + 1, y, Tile.Arr(new('=', BAR), ABGR.Gray, b));
                            sf_ui.Print(x + 1, y, Tile.Arr(new('=', BAR * hp.hp / hp.maxHP), f, b));
                            sf_ui.Print(x + 1 + BAR, y, Tile.Arr($"] HP: {hp.hp}", f, b));
                            break;
                        }
                }
            }
        }
        //Print Player
        {
            int x = 2;
            int y = 2;

            Sf.DrawRect(sf_ui, x++, y++, wd, 24, new() {
                f = ABGR.White,
                b = ABGR.SetA(ABGR.Black, 128),
            });

            var b = ABGR.Black;
            var ship = player.ship;
            var devices = ship.devices;
            var solars = devices.Solar;
            var reactors = devices.Reactor;
            var weapons = devices.Weapon;
            var shields = devices.Shield;
            var misc = devices.Installed.OfType<Service>(); {
                double totalFuel = reactors.Sum(r => r.energy),
                       maxFuel = reactors.Sum(r => r.desc.capacity),
                       netDelta = reactors.Sum(r => r.energyDelta),
                       totalSolar = solars.Sum(s => s.maxOutput);
                Tile[] bar;
                if (totalFuel > 0) {
                    (var arrow, var f) = netDelta switch {
                        < 0 => ('<', ABGR.Yellow),
                        > 0 => ('>', ABGR.Cyan),
                        _   => ('=', ABGR.White),
                    };
                    int length = (int)Ceiling(BAR * totalFuel / maxFuel);
                    bar = [
                        ..Tile.Arr(new string('=', length - 1) + arrow, f, b),
                        ..Tile.Arr(new string('=', BAR - length), ABGR.Gray, b)
                    ];
                } else {
                    bar = Tile.Arr(new string('=', BAR), ABGR.Gray, b);
                }
                int totalUsed = player.energy.totalOutputUsed,
                    totalMax = player.energy.totalOutputMax;
                int l;
                l = (int)Ceiling(BAR * (double)totalUsed / Max(1, totalMax));

                Array.Copy(bar[..l].Select(t => t with { Background = ABGR.DarkKhaki }).ToArray(), bar, l);
                l = (int)Min(bar.Length, Ceiling(BAR * (double)totalSolar / Max(1, totalMax)));

				Array.Copy(bar[..l].Select(t => t with { Background = ABGR.DarkCyan }).ToArray(), bar, l);
                sf_ui.Print(x, y++, [
                    ..Tile.Arr("[", 0xFFFFFFFF, b),
                    ..bar,
                    ..Tile.Arr("]", 0xFFFFFFFF, b),
                    (0, 0, ' '),
                    ..Tile.Arr($"{totalUsed,3}/{totalMax,3} Total Capacity", 0xFFFFFFFF, b)
                    ]
                    );
            }
            if (reactors.Any()) {
                foreach (var reactor in reactors) {
                    Tile[] bar;
                    if (reactor.energy > 0) {
                        (var arrow, var f) = reactor.energyDelta switch {
                            < 0 => ('<', ABGR.Yellow),
                            > 0 => ('>', ABGR.Cyan),
                            _ => ('=', ABGR.White)
                        };
                        int length = (int)Ceiling(BAR * reactor.energy / reactor.desc.capacity);
                        bar = [
                            ..Tile.Arr(new string('=', length - 1) + arrow, f, b),
                            ..Tile.Arr(new string('=', BAR - length), ABGR.Gray, b)
                            ];
                    } else {
                        bar = Tile.Arr(new string('=', BAR), ABGR.Gray, b);
                    }
                    var delta = -reactor.energyDelta;
                    if(delta == -0) {
                        delta = 0;
                    }

                    var name = Tile.Arr($"{delta,3}/{reactor.maxOutput,3} {reactor.source.type.name}", reactor.energy > 0 ? ABGR.White : ABGR.Gray, b);
                    Tile[] entry = [
                        .. Tile.Arr($"[", ABGR.White, b),
                        ..bar,
                        .. Tile.Arr($"] ", ABGR.White, b),
                        .. name
                    ];


					int l = (int)Ceiling(BAR * (double)-reactor.energyDelta / reactor.maxOutput);
                    for (int i = 0; i < l; i++) {
                        ref var e = ref entry[i + 1];
                        e = e with { Background = ABGR.DarkKhaki };
                    }
                    sf_ui.Print(x, y, entry);
                    y++;
                }
                //y++;
            }
            if (solars.Any()) {
                foreach (var s in solars) {
                    int length = (int)Ceiling(BAR * (double)s.maxOutput / s.desc.maxOutput);
                    int sublength = s.maxOutput > 0 ? (int)Ceiling(length * (-s.energyDelta) / s.maxOutput) : 0;
                    var f = ABGR.White;
                    Tile[] line = [
                        new Tile(f, b, '['),
                        ..Tile.Arr(new string('=', sublength), ABGR.Yellow, ABGR.DarkKhaki),
                        ..Tile.Arr(new string('=', length - sublength), ABGR.Cyan, b),
                        ..Tile.Arr(new string('=', BAR - length), ABGR.Gray, b),
                        new Tile(f, b, ']'),
                        Tile.empty,
						..Tile.Arr($"{Abs(s.energyDelta),3}/{s.maxOutput,3}", f, b),
                        Tile.empty,
                        ..Tile.Arr(s.source.type.name, f, b)
						];
                    sf_ui.Print(x, y, line);
                    y++;
                }
                
            }
			y++;
            if (misc.Any()) {
                foreach (var m in misc) {
                    var tag = m.source.type.name;
                    var f = ABGR.White;
                    sf_ui.Print(x, y, Tile.Arr($"[{new string('-', BAR)}] {tag}", f, b));
                    y++;
                }
                //y++;
            }
            if (shields.Any()) {
                foreach (var s in shields.Reverse<Shield>()) {
                    var f = player.energy.off.Contains(s) ? ABGR.Gray :
                        s.hp == 0 || s.delay > 0 ? ABGR.Yellow :
                        s.hp < s.desc.maxHP ? ABGR.Cyan :
						ABGR.White;

                    Tile[] bar;
                    if(s.delay > 0) {
                        var l = (int)(BAR * s.delay / s.desc.depletionDelay);
                        bar = [
                            new Tile(f, b, '['),
                            ..Tile.Arr(new string('=', BAR - l), ABGR.Gray, b),
                            ..Tile.Arr(new string (' ', l), f, b),
                            new Tile(f, b, ']')
                            ];
                    } else {
                        var l = BAR * s.hp / s.desc.maxHP;
						bar = [
	                        new Tile(f, b, '['),
							..Tile.Arr(new string('=', l), f, b),
							..Tile.Arr(new string ('=', BAR - l), ABGR.Gray, b),
							new Tile(f, b, ']')
	                    ];
                    }
                    var counter = Tile.Arr($"{s.hp,3}/{s.desc.maxHP,3}", f, b);
					var name = Tile.Arr(s.source.type.name, f, b);
					sf_ui.Print(x, y, [
                        ..bar,
                        Tile.empty,
                        ..counter,
                        Tile.empty,
                        ..name
                        ]);
                    y++;
                }
            }
            switch (player.hull) {
                case LayeredArmor las: {
                        foreach (var armor in las.layers.Reverse<Armor>()) {
                            //Foreground describes current action
                            var f =
                                    armor.hp > 0 ?
                                        (armor.corrode.Any() ?
											ABGR.Red :
                                        tick - armor.lastDamageTick < 15 ?
											ABGR.Yellow :
                                        tick - armor.lastRegenTick < 15 ?
											ABGR.Cyan :
											ABGR.White) :
										ABGR.Gray;
                            //Background describes capability
                            var bb =

                                armor.hasRecovery ?
									ABGR.Blend(ABGR.Black,ABGR.SetA(ABGR.Cyan,51)) :
                                armor.corrode.Any() ?
									ABGR.Blend(ABGR.Black,ABGR.SetA(ABGR.Red,51)) :
                                armor.hp > 0 ?
                                    b :
                                armor.canAbsorb ?
									ABGR.Blend(ABGR.Black, ABGR.SetA(ABGR.White,51)) :
                                    b;
                            var available = BAR * Min(armor.maxHP, armor.desc.maxHP) / Max(1, armor.desc.maxHP);
                            var active = available * Min(armor.hp, armor.maxHP) / Max(1, armor.maxHP);

                            var hp = armor.hp;
                            var bonus = armor.apparentHP - hp;
                            Tile[] bar = [
                                ..Tile.Arr("[", f, bb),
                                ..Tile.Arr(new string('=', active), f, bb),
                                ..Tile.Arr(new string('=', available - active), ABGR.Gray, bb),
                                ..Tile.Arr(new string(' ', BAR - available), f, bb),
                                ..Tile.Arr("]", f, bb)
                                ];
                            var counter = $"{(bonus > 0 ? $"{bonus}+{hp}" : $"{hp}"),3}/{armor.maxHP,3}";
                            var name =      armor.source.type.name;
                            sf_ui.Print(x, y, [..bar, Tile.empty, ..Tile.Arr($"{counter} {name}", f, bb)]);
                            y++;
                        }
                        break;
                    }
                case HP hp: {
                        var f = ABGR.White;
                        var l = BAR * hp.hp / hp.maxHP;

                        
                        sf_ui.Print(x, y, Tile.ArrFrom(XElement.Parse(@$"
<S f=""{f}"" b=""{b}"">[{new string('=', l)}<S b=""{ABGR.Gray}"">{new string('=', BAR - l)}</S>] HP: {hp.hp}</S>"""))
);
                        y++;
                        break;
                    }
            }

            y++;


			if(weapons.Any()) {
				int i = 0;
				foreach(var w in weapons) {
					var arrow =
						i == player.primary.index ?
							"->" :
						i == player.secondary.index ?
							"=>" :
							"  ";
					var name = w.GetReadoutName();
					string enhancement = w.mod != FragmentMod.EMPTY ? "+" : "";
					var tag = $"{arrow} {name} {enhancement}";
					var foreground =
						player.energy.off.Contains(w) ?
							ABGR.Gray :
						w.firing || w.delay > 0 ?
							ABGR.Yellow :
						ABGR.White;
					sf_ui.Print(x, y, [.. Tile.Arr("[", ABGR.White, b), .. w.GetBar(BAR), .. Tile.Arr(tag, foreground, b)]);
					y++;
					i++;
				}
				y++;
			}
            var (_f, _b) = (ABGR.White, ABGR.Black);
            sf_ui.Print(x, y++, Tile.Arr($"Stealth: {ship.stealth:0.00}", _f, _b));
            sf_ui.Print(x, y++, Tile.Arr($"Visibility: {SStealth.GetVisibleRangeOf(player):0.00}", _f, _b));
            sf_ui.Print(x, y++, Tile.Arr($"Darkness: {player.ship.silence:0.00}", _f, _b));
        }
        Draw?.Invoke(sf_ui);
    }
}
public class Edgemap {
	public Action<Sf> Draw { set; get; }
	public int Width => sf.GridWidth;
    public int Height => sf.GridHeight;
    Camera camera;
    PlayerShip player;
    public double viewScale;
    public Sf sf;
    public Edgemap(Monitor m){
        this.sf = new Sf(m.Width, m.Height, Fonts.FONT_8x8);
        this.camera = m.camera;
        this.player = m.playerShip;
        viewScale = 1;
    }
    public void Update(TimeSpan delta) {
    }
    public void Render(TimeSpan drawTime) {
        sf.Clear();
        var screenSize = new XY(Width - 1, Height - 1);
        var screenCenter = screenSize / 2;
        var halfWidth = Width / 2;
        var halfHeight = Height / 2;
        var range = 192;

        foreach(var (entity, dist) in player.world.entities.SelectKeyValue<(Entity entity, double dist)?>(
            ((int, int) p) => (player.position - p).maxCoord < range,
            entity => (entity is ISegment { parent: { } par } ? par : entity) is { tile: { } } ent && player.GetVisibleDistanceLeft(ent) is { } d and > 0 ? (entity, d) : null).OfType<(Entity entity, double dist)>()
            ) {
			var offset = (entity.position - player.position).Rotate(-camera.rotation);
			var (x, y) = (offset / viewScale).abs;
			if(x >= halfWidth || y >= halfHeight) {
				(x, y) = Main.GetBoundaryPoint(screenSize, offset.angleRad);
				PrintTile(x, y, dist, entity);
			} else if(x > halfWidth - 4 || y > halfHeight - 4) {
				(x, y) = screenCenter + offset / viewScale;// + new XY(1, 1);
				PrintTile(x, y, dist, entity);
			}
		};
        if(player.active) {
            var (x, y) = Main.GetBoundaryPoint(screenSize, player.rotationRad);
            sf.Tile[x, Height - y - 1] = new Tile(ABGR.White, ABGR.Transparent, 7);
        }
        void PrintTile(int x, int y, double distance, Entity e) {
            switch(e) {
                case ISegment:
                case ActiveObject:
                case Projectile:
                case Wreck:
                    var c = e.tile.Foreground;
                    const int threshold = 16;
                    if (distance < threshold) {
                        var a = (byte)(255 * distance / threshold);
						c = ABGR.SetA(c, a);
                    }
                    sf.Tile[x, Height - y - 1] = new Tile(c, ABGR.Transparent, 254);
                    break;
                default: return;
            }
        }
        Draw?.Invoke(sf);
    }
}
public class Minimap {
	public Action<Sf> Draw { set; get; }
	PlayerShip player;
    public int size;
    Camera camera;
    public double time;
    public byte alpha;
    List<(int x, int y)> area = new();
    public Sf sf;
    int Width => sf.GridWidth;
    int Height=>sf.GridHeight;
	XY screenSize, screenCenter;
    public Minimap(Monitor m) {
        this.player = m.playerShip;
        this.size = 16;
		this.sf = new Sf(size, size, Fonts.FONT_8x8) { pos = (m.Width - 16 - 2, 2) };
		this.camera = m.camera;
		screenSize = new(Width, Height);
        screenCenter = screenSize / 2;

        alpha = 255;

        var center = new XY(Width, Height) / 2;
        area = new(Width.AsEnumerable()
            .SelectMany(x => (0..Height).Select(y => (x, y)))
            .Where(((int x, int y) p) => true || center.Dist(new(p.x, p.y)) < Width / 2));
    }
    public void Update(TimeSpan delta) {
        time += delta.TotalSeconds;
    }
    public void Render(TimeSpan delta) {
        var halfSize = size / 2;
        var range = 192;
        var mapScale = (range / halfSize);
        var mapSample = player.world.entities.space.DownsampleSet(mapScale);
        var scaledEntities = player.world.entities.TransformSelectList<(Entity entity, double distance)?>(
            e => (screenCenter + ((e.position - player.position) / mapScale).Rotate(-camera.rotation)).flipY + (0, Height),
            ((int x, int y) p) => p is (> -1, > -1) && p.x < Width && p.y < Height,
            ent => ent is { tile:not null } and not ISegment && player.GetVisibleDistanceLeft(ent) is var dist && dist > 0 ? (ent, dist) : null
        );

        var b = ABGR.RGBA(102, 102, 102, (byte)((153 * alpha) / 255));
        uint f;
        char g;
        foreach(var(x, y) in area) {
            if (scaledEntities.TryGetValue((x, y), out var entities)) {
                (var entity, var distance) = entities[(int)time % entities.Count()].Value;

                g = (char)entity.tile.Glyph;
                f = entity.tile.Foreground;

                const double threshold = 16;
                if (distance < threshold) {
                    f = ABGR.SetA(f, (byte)(255 * distance / threshold));
                }
            } else {
                f = ABGR.SetA(ABGR.White, (byte)(51 + ((x + y) % 2 == 0 ? 0 : 12)));
                g = '#';
			}
            f = ABGR.SetA(f, (byte)((ABGR.A(f) * alpha) / 255));
			sf.Tile[x, y] = new Tile(f, b, g);
		}
        Draw?.Invoke(sf);
    }
}
public class CommunicationsWidget {
	public bool visible = true;
	PlayerShip playerShip;
    int ticks;
    CommandMenu? menu;
    public Sf sf;
    public CommunicationsWidget(Sf Surface, PlayerShip playerShip) {
		this.playerShip = playerShip;
        menu = null;
    }
    public void Update(TimeSpan delta) {
        if (menu?.visible == true) {
            menu.Update(delta);
            return;
        }
        if (ticks % 30 == 0) {
            playerShip.wingmates.RemoveAll(w => !w.active);
        }
        ticks++;
    }
    public void ProcessKeyboard(KB info) {
        if (menu?.visible == true) {
            return;
        }
        foreach (var k in info.Press) {
            int index = SMenu.keyToIndex((char)k);
            if (index > -1 && index < 10 && index < playerShip.wingmates.Count) {
                menu = new(sf, playerShip, playerShip.wingmates[index]);
                //menu.Surface.Position = Surface.Position;
			}
        }
    }
    public void Render(TimeSpan delta) {
        if (menu?.visible == true) {
            menu.Render(delta);
            return;
        }
        int x = 0;
        int y = 0;

        sf.Clear();

        var f = ABGR.White;
        if (ticks % 60 < 30) {
            f = ABGR.Yellow;
        }
        var b = ABGR.Black;
        sf.Print(x, y++, Tile.Arr("[Communications]", f, b));
        //this.Print(x, y++, "[Ship control locked]", foreground, back);
        sf.Print(x, y++, Tile.Arr("[ESC     -> cancel]", f, b));
        y++;
        /*
        if (playerShip.wingmates.Count(w => w.active) == 0) {
            playerShip.wingmates.AddRange(playerShip.world.entities.all.OfType<AIShip>());
            foreach (var w in playerShip.wingmates) {
                w.ship.sovereign = playerShip.sovereign;
            }
        }
        */

        int index = 0;
        foreach (var w in playerShip.wingmates.Take(10)) {
            char key = SMenu.indexToKey(index++);
            sf.Print(x, y++, Tile.Arr($"[{key}] {w.name}: {w.behavior.GetOrderName()}", ABGR.White, ABGR.Black));
        }
    }
    public class CommandMenu {

        //PlayerShip player;
        AIShip subject;
        public int ticks = 0;
        private Dictionary<string, Action> commands;
        public Sf Surface;
        public bool visible = true;
        public CommandMenu(Sf Surface, PlayerShip player, AIShip subject) {
            this.Surface = Surface;
			//this.player = player;
			this.subject = subject;
            EscortShip GetEscortOrder(int i) {
                int root = (int)Sqrt(i);
                int lower = root * root;
                int upper = (root + 1) * (root + 1);
                int range = upper - lower;
                int index = i - lower;
                return new EscortShip(player, XY.Polar(
                        -(PI * index / range), root * 2));
            }
            commands = new();
            switch(subject.behavior) {
                case Wingmate w:
                    commands["Form Up"] = () => {
                        player.AddMessage(new Transmission(subject, $"Ordered {subject.name} to Form Up"));
                        w.order = GetEscortOrder(0);
                    };

                    if(subject.devices.Weapon.FirstOrDefault(w => w.projectileDesc.tracker != 0) is Weapon weapon) {
                        commands["Fire Tracker"] = () => {
                            if (!player.GetTarget(out ActiveObject target)) {
                                player.AddMessage(new Transmission(subject, $"{subject.name}: Firing tracker at nearby enemies"));
                                w.order = new FireTrackerNearby(weapon);
                                return;
                            }
                            player.AddMessage(new Transmission(subject, $"{subject.name}: Firing tracker at target"));
                            w.order = new FireTrackerAt(weapon, target);
                        };
                    }
                    commands["Attack Target"] = () => {
                        if (player.GetTarget(out ActiveObject target)) {
                            w.order = new AttackTarget(target);
                            player.AddMessage(new Transmission(subject, $"{subject.name}: Attacking target"));
                        } else {
                            player.AddMessage(new Transmission(subject, $"No target selected"));
                        }
                    };
                    commands["Wait"] = () => {
                        w.order = new GuardAt(new TargetingMarker(player, "Wait", subject.position));
                        player.AddMessage(new Transmission(subject, $"Ordered {subject.name} to Wait"));
                    };
                    break;
                default:
                    commands["Form Up"] = () => {
                        player.AddMessage(new Message($"Ordered {subject.name} to Form Up"));
                        subject.behavior = GetEscortOrder(0);
                    };
                    commands["Attack Target"] = () => {
                        if (player.GetTarget(out ActiveObject target)) {
                            var attack = new AttackTarget(target);
                            var escort = GetEscortOrder(0);
                            subject.behavior = attack;
                            OrderOnDestroy.Register(subject, attack, escort, target);
                            player.AddMessage(new Message($"Ordered {subject.name} to Attack Target"));
                        } else {
                            player.AddMessage(new Message($"No target selected"));
                        }
                    };
                    break;
            }
        }
        public void Update(TimeSpan delta) {
            ticks++;
        }
        public void ProcessKeyboard(KB info) {
            foreach (var k in info.Press) {
                int index = SMenu.keyToIndex((char)k);
                if (index > -1 && index < commands.Count) {
                    commands.Values.ElementAt(index)();
                }
            }
            if (info.IsPress(KC.Escape)) {
                visible = false;
            }
        }
        public void Render(TimeSpan delta) {
            int x = 0;
            int y = 0;

            Surface.Clear();

            var f = ABGR.White;
            if (ticks % 60 < 30) {
                f = ABGR.Yellow;
            }
            var b = ABGR.Black;
            Surface.Print(x, y++, Tile.Arr("[Command]", f, b));
            //this.Print(x, y++, "[Ship control locked]", foreground, back);
            Surface.Print(x, y++, Tile.Arr("[ESC     -> cancel]", f, b));
            y++;
            Surface.Print(x, y++, Tile.Arr($"{subject.name}:{subject.behavior.GetOrderName()}", ABGR.White, ABGR.Black));
            y++;
            int index = 0;
            foreach (var w in commands.Keys) {
                char key = SMenu.indexToKey(index++);
                Surface.Print(x, y++, Tile.Arr($"[{key}] {w}", ABGR.White, ABGR.Black));
            }
        }
    }
}
public class PowerWidget {
    public Action<Sf> Draw { get; set; }

    public bool visible = true;
    PlayerShip playerShip;
    Mainframe main;
    int ticks;
    private bool _blockMouse;
    public Sf sf;
    public bool blockMouse {
        set {
            _blockMouse = value;
        }
        get => _blockMouse;
    }
    public PowerWidget(Mainframe main) {
        this.playerShip = main.playerShip;
        sf = new(36, 16, Fonts.FONT_6x8) { pos = (3, 32) };
		this.main = main;
        InitButtons();
    }
    List<LabelButton> buttons = [];
    public void InitButtons() {
        int x = 4;
        int y = 6;
        sf.Clear();
        foreach (var p in playerShip.powers) {
            buttons.Add(new LabelButton(sf, (x, y++), p.type.name) {
                leftHold = () => {
                    if (p.ready) {
                        //Enable charging
                        p.charging = true;
                    }
                }
            });
        }
    }
    public void Update(TimeSpan delta) {
        ticks++;

        bool charging = false;
        foreach (var p in playerShip.powers) {
            if (p.charging) {
                //We don't need to check ready because we already do that before we set charging
                //Charging up
                p.invokeCharge += (int)(60 * delta.TotalSeconds);

                charging = true;
                if (ticks % 3 == 0) {
                    p.charging = false;
                }
            } else if (p.invokeCharge > 0) {
                if (p.invokeCharge < p.invokeDelay) {
                    p.invokeCharge -= (int)(60 * delta.TotalSeconds);
                } else {
                    //Invoke now!
                    p.cooldownLeft = p.cooldownPeriod;

                    p.type.Effect.ForEach(e => {
                        if (e is PowerJump j) {
                            j.Invoke(playerShip);
                            main.Jump();
                        } else {
                            e.Invoke(playerShip);
                        }
                    });
                    if (p.type.message != null) {
                        playerShip.AddMessage(new Message(p.type.message));
                    }

                    main.audio.PlayPowerRelease();

                    //Reset charge
                    p.invokeCharge = 0;
                    p.charging = false;
                }
            }
        }

        if(charging) {
            if (!main.audio.powerCharge.playing) {
                main.audio.PlayPowerCharge();
            }
        }
    }
    public void HandleKey(KB kb) {
        foreach (var k in kb.Down) {
            //If we're pressing a digit/letter, then we're charging up a power
            int powerIndex = SMenu.keyToIndex((char)k);
            //Find the power
            if (powerIndex > -1 && powerIndex < playerShip.powers.Count) {
                var power = playerShip.powers[powerIndex];
                //Make sure this power is available
                if (power.ready) {
                    //Enable charging
                    power.charging = true;
                }
            }
        }
        if (kb.IsPress(KC.Escape)) {
            //Set charge for all powers back to 0
            foreach (var p in playerShip.powers) {
                p.invokeCharge = 0;
                p.charging = false;
            }
            //Hide menu
            //IsVisible = false;
        }
        if (kb.IsPress(KC.I)) {
            //Set charge for all powers back to 0
            foreach (var p in playerShip.powers) {
                if (p.invokeCharge < p.invokeDelay) {
                    p.invokeCharge = 0;
                    p.charging = false;
                }
            }
            //Hide menu
            //IsVisible = false;
        }
    }
    public void HandleMouse(HandState state) {

    }
    public void Render(TimeSpan delta) {

        int x = 0;
        int y = 0;
        int index = 0;
        sf.Clear();

        Sf.DrawRect(sf, x++, y++, sf.GridWidth, sf.GridHeight, new() {

        });
        var foreground = ABGR.White;
        if (ticks % 60 < 30) {
            foreground = ABGR.Yellow;
        }
        var back = ABGR.Black;
        sf.Print(x, y++, "[Powers]", foreground, back);
        //this.Print(x, y++, "[Ship control locked]", foreground, back);
        sf.Print(x, y++, "[ESC     -> cancel]", foreground, back);
        sf.Print(x, y++, "[P       -> close ]", foreground, back);
        sf.Print(x, y++, "[Hold    -> charge]", foreground, back);
        sf.Print(x, y++, "[Release -> invoke]", foreground, back);
        y++;

        var bl = ABGR.Black;
        var gr = ABGR.Gray;
        var wh = ABGR.White;
        foreach (var p in playerShip.powers) {
            char key = SMenu.indexToKey(index);
            if (p.cooldownLeft > 0) {
                int chargeBar = (int)(16 * p.cooldownLeft / p.cooldownPeriod);
                sf.Print(x, y++, [
                    ..Tile.Arr($"[{key}] {p.type.name,-8} ", gr, bl),
					..Tile.Arr("[", wh, bl),
					..Tile.Arr(new string('>', 16 - chargeBar), wh, bl),
					..Tile.Arr(new string('>', chargeBar), gr, bl),
					..Tile.Arr("]", wh, bl)
                    ]);
            } else if (p.invokeCharge > 0) {
                var chargeMeter = (int)Min(16, 16 * p.invokeCharge / p.invokeDelay);

                var c = ABGR.Yellow;
                if (p.invokeCharge >= p.invokeDelay && ticks % 30 < 15) {
                    c = ABGR.Orange;
                }
                sf.Print(x, y++, [
					..Tile.Arr($"[{key}] {p.type.name,-8} ", c, bl),
                    ..Tile.Arr("[", c, bl),
                    ..Tile.Arr(new string('>', chargeMeter), c, bl),
                    ..Tile.Arr(new string('>', 16 - chargeMeter), wh, bl),
                    ..Tile.Arr("]", c, bl)
                    ]);
            } else {
                sf.Print(x, y++, [
					..Tile.Arr($"[{key}] {p.type.name,-8} ", wh, bl),
					..Tile.Arr($"[{new string('>', 16)}]", wh, bl)]);
            }
            index++;
        }

        //this.SetCellAppearance(Width/2, Height/2, new ColoredGlyph(Color.White, Color.White, 'X'));

        Draw?.Invoke(sf);
    }

}
