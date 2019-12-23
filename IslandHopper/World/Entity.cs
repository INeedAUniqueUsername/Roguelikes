﻿using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IslandHopper.Constants;

namespace IslandHopper {
    /*
    public interface Visible {
        XYZ Position { get; set; }
        ColoredGlyph SymbolCenter { get; }
        bool Active { get; }
    }
    */
	public interface Entity : Effect {
        Island World { get; }
		//XYZ Position { get; set; }			//Position in meters
		XYZ Velocity { get; set; }			//Velocity in meters per step
		//bool Active { get; }                    //	When this is inactive, we remove it
		void OnRemoved();
		//void UpdateRealtime(TimeSpan delta);				//	For step-independent effects
		//void UpdateStep();					//	The number of steps per one in-game second is defined in Constants as STEPS_PER_SECOND

		//ColoredGlyph SymbolCenter { get; }
		ColoredString Name { get; }
	}
    public interface Damageable {
        void OnDamaged(Damager source);
    }
    public interface Damager {
        ColoredString Name { get; }
        int damage { get; }
        double knockback { get; }
    }
	public static class EntityHelper {
		public static bool OnGround(this Entity g) => g.World.voxels.InBounds(g.Position) && (g.World.voxels[g.Position].Collision == VoxelType.Floor || g.World.voxels[g.Position.PlusZ(-0.8)].Collision == VoxelType.Solid);
		public static void UpdateGravity(this Entity g) {
            g.UpdateFriction();
            //	Fall or hit the ground
            if (g.OnGround()) {
                if(g.Velocity.z < 0) {
                    g.Velocity.z = 0;
                }
                if (g is Player) {
                    int a = 5;
                }
			} else {
				Debug.Print("fall");
				g.Velocity += new XYZ(0, 0, -9.8 / STEPS_PER_SECOND);
			}
		}
		//We attempt to enforce continuous collision detection by incrementing the motion in small steps
		private static XYZ CalcMotionStep(XYZ Velocity) {
			if (Velocity.Magnitude < 1) {
				return Velocity / 4;
			} else {
				return Velocity.Normal / 4;
			}
		}
		private static void UpdateFriction(this Entity g) {
			//Ground friction
			if (g.OnGround()) {
				g.Velocity.x *= 0.9;
				g.Velocity.y *= 0.9;
			}
		}
		public static void UpdateMotion(this Entity g) {
			if(g.Velocity < 0.1) {
				return;
			}
			XYZ step = CalcMotionStep(g.Velocity);
			XYZ final = g.Position;
			for (XYZ p = g.Position + step; (g.Position - p).Magnitude < g.Velocity.Magnitude; p += step) {
				if (g.World.voxels.Try(p) is Air) {
					final = p;
				} else {
					break;
				}
			}
			/*
			//The velocity is the displacement that we were supposed to travel for this step
			//This is the average velocity for the actual displacement we traveled in this step
			Point3 velocityAverage = (final - g.Position);
			//This is the displacement that we did not get to travel this tick
			Point3 velocityDelta = velocityAverage - g.Velocity;
			*/
			g.Position = final;
		}
		public static void UpdateMotionCollision(this Entity g, Func<Entity, bool> ignoreEntityCollision = null, Func<Voxel, bool> ignoreTileCollision = null) {
            /*
            if (g.Velocity < 0.1) {
				return;
			}
            */
            //ignoreEntityCollision = ignoreEntityCollision ?? (e => true);
            //ignoreTileCollision = ignoreTileCollision ?? (v => false);
			XYZ step = CalcMotionStep(g.Velocity);
			XYZ final = g.Position;
			for (XYZ p = g.Position + step; (g.Position - p).Magnitude < g.Velocity.Magnitude; p += step) {
				var v = g.World.voxels.Try(p);
				if (v is Air || ignoreTileCollision?.Invoke(v) == true) {
					if(ignoreEntityCollision != null) {
						var entities = g.World.entities.Try(p).Where(e => !ReferenceEquals(e, g));
                        foreach(var entity in entities) {
                            if(!ignoreEntityCollision(entity)) {
                                goto Done;
                            }
                        }
                        final = p;
                    } else {
						final = p;
					}
				} else {
					break;
				}
			}
            Done:
			g.Position = final;
		}
        public static void UpdateMotionCollisionTrail(this Entity g, out HashSet<XYZ> trail, Func<Entity, bool> ignoreEntityCollision = null, Func<Voxel, bool> ignoreTileCollision = null) {
            trail = new HashSet<XYZ>(new XYZGridComparer());
            /*
            if (g.Velocity < 0.1) {
                return;
            }
            */
            //ignoreEntityCollision = ignoreEntityCollision ?? (e => true);
            //ignoreTileCollision = ignoreTileCollision ?? (v => false);
            XYZ step = CalcMotionStep(g.Velocity);
            XYZ final = g.Position;
            trail.Add(final.i);
            for (XYZ p = g.Position + step; (g.Position - p).Magnitude < g.Velocity.Magnitude; p += step) {
                var v = g.World.voxels.Try(p);
                if (v is Air || ignoreTileCollision?.Invoke(v) == true) {
                    if (ignoreEntityCollision != null) {
                        var entities = g.World.entities.Try(p).Where(e => !ReferenceEquals(e, g));
                        foreach(var entity in entities) {
                            if(!ignoreEntityCollision(entity)) {
                                goto Done;
                            }
                        }
                        final = p;
                        trail.Add(final.i);
                    } else {
                        final = p;
                        trail.Add(final.i);
                    }
                } else {
                    break;
                }
            }
            Done:
            g.Position = final;
        }
        public static void Witness(this Entity e, WorldEvent we) {
			if (e is Witness w)
				w.Witness(we);
		}
	}
	interface Witness {
		void Witness(WorldEvent e);
	}
	public class Player : Entity, Witness {
		public XYZ Velocity { get; set; }
		public XYZ Position { get; set; }
		public Island World { get; set; }
		public HashSet<EntityAction> Actions { get; private set; }
		public HashSet<IItem> Inventory { get; private set; }
        public HashSet<Effect> Watch { get; private set; }
		public List<HistoryEntry> HistoryLog { get; }	//All events that the player has witnessed
		public List<HistoryEntry> HistoryRecent { get; }   //Events that the player is currently witnessing


		public class HistoryEntry {
            public ColoredString Desc => times == 1 ? _desc : (_desc + new ColoredString($" (x{times})", Color.White, Color.Black));
            public ColoredString _desc;
            public int times;
			public double ScreenTime;
			public HistoryEntry(ColoredString Desc, double ScreenTime = 4) {
				this._desc = Desc;
				this.ScreenTime = ScreenTime;
                this.times = 1;
			}
            public void SetScreenTime(double ScreenTime = 4) {
                this.ScreenTime = ScreenTime;
            }
		}

		public int frameCounter = 0;

		public Player(Island World, XYZ Position) {
			this.World = World;
			this.Position = Position;
			this.Velocity = new XYZ(0, 0, 0);
			Actions = new HashSet<EntityAction>();
			Inventory = new HashSet<IItem>();
            Watch = new HashSet<Effect>();

			HistoryLog = new List<HistoryEntry>();
			HistoryRecent = new List<HistoryEntry>();

			World.AddEntity(new Parachute(this));
		}
		public bool AllowUpdate() => Actions.Count > 0 || frameCounter > 0;
		public bool Active => true;
		public void OnRemoved() { }
		public void UpdateRealtime(TimeSpan delta) {
            HistoryRecent.RemoveAll(e => (e.ScreenTime -= delta.TotalSeconds) < 0);
            foreach(var i in Inventory) {
                i.UpdateRealtime(delta);
            }
		}
		public void UpdateStep() {
            if (frameCounter > 0)
                frameCounter--;

            this.UpdateGravity();
            this.UpdateMotion();

            foreach (var a in Actions) {
                a.Update();
            }
			Actions.RemoveWhere(a => a.Done());
            
			foreach(var i in Inventory) {
                //Copy so that when the item updates motion, the change does not apply to the player
				i.Position = Position.copy;
				i.Velocity = Velocity.copy;
                i.UpdateStep();
			}
            
            Inventory.RemoveWhere(i => !i.Active);
            Watch.RemoveWhere(t => !t.Active);
			if(!this.OnGround())
				frameCounter = 20;

			//HistoryRecent.RemoveAll(e => e.ScreenTime < 1);
		}

		public void Witness(WorldEvent e) {
            var desc = e.Desc;
            if(HistoryLog.Count == 0) {
                var entry = new HistoryEntry(desc);
                HistoryLog.Add(entry);
                HistoryRecent.Add(entry);
            } else {
                var last = HistoryLog.Last();
                if(last._desc.ToString() == desc.ToString()) {
                    last.times++;
                    last.SetScreenTime();

                    if(HistoryRecent.Count == 0 || HistoryRecent.Last()._desc.ToString() != desc.ToString()) {
                        HistoryRecent.Add(last);
                    }
                } else {
                    var entry = new HistoryEntry(desc);
                    HistoryLog.Add(entry);
                    HistoryRecent.Add(entry);
                }
            }
        }

		public ColoredGlyph SymbolCenter => new ColoredString("@", Color.White, Color.Black)[0];
		public ColoredString Name => new ColoredString("Player", Color.White, Color.Black);
	}

	/*
	class Human : Entity {
		public Point3 Velocity { get; set; }
		public Point3 Position { get; set; }
		public GameConsole World { get; private set; }
		public HashSet<PlayerAction> Actions;
		public Human(GameConsole World, Point3 Position) {
			this.World = World;
			this.Position = Position;
			this.Velocity = new Point3(0, 0, 0);
			Actions = new HashSet<PlayerAction>();
		}
		public bool IsActive() => true;
		public bool OnGround() => (World.voxels[Position] is Floor || World.voxels[Position.PlusZ(-1)] is Grass);
		public void UpdateRealtime(TimeSpan delta) {

		}
		public void UpdateStep() {
			//	Fall or hit the ground
			if(Velocity.z < 0 && OnGround()) {
				Velocity.z = 0;
			} else {
				System.Console.WriteLine("fall");
				Velocity += new Point3(0, 0, -9.8 / 30);
			}
			Point3 normal = Velocity.Normal();
			Point3 dest = Position;
			for(Point3 p = Position + normal; (Position - p).Magnitude() < Velocity.Magnitude(); p += normal) {
				if(World.voxels[p] is Air) {
					dest = p;
				} else {
					break;
				}
			}
			Position = dest;
			Actions.ToList().ForEach(a => a.Update());
			Actions.RemoveWhere(a => a.Done());
		}

		public static readonly ColoredString symbol = new ColoredString("U", Color.White, Color.Transparent);
		public virtual ColoredString GetSymbolCenter() => symbol;
	}
	class Player : Human {

		public Player(GameConsole World, Point3 Position) : base(World, Position) { }
		public bool AllowUpdate() => Actions.Count > 0;
		public static ColoredString symbol_player = new ColoredString("@", Color.White, Color.Transparent);
		public override ColoredString GetSymbolCenter() => symbol_player;
	}
	*/
}
