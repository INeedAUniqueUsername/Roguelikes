﻿using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IslandHopper.Constants;

namespace IslandHopper {
	interface Entity {
		World World { get; }
		Point3 Position { get; set; }			//Position in meters
		Point3 Velocity { get; set; }			//Velocity in meters per step
		bool Active { get; }                    //	When this is inactive, we remove it
		void OnRemoved();
		void UpdateRealtime();				//	For step-independent effects
		void UpdateStep();					//	The number of steps per one in-game second is defined in Constants as STEPS_PER_SECOND

		ColoredGlyph SymbolCenter { get; }
		ColoredString Name { get; }
	}
	static class SGravity {
		public static bool OnGround(this Entity g) => (g.World.voxels[g.Position].Collision == VoxelType.Floor || g.World.voxels[g.Position.PlusZ(-1)].Collision == VoxelType.Solid);
		public static void UpdateGravity(this Entity g) {
			//	Fall or hit the ground
			if (g.Velocity.z < 0 && g.OnGround()) {
				g.Velocity.z = 0;
			} else {
				Debug.Print("fall");
				g.Velocity += new Point3(0, 0, -9.8 / STEPS_PER_SECOND);
			}
		}
		public static void UpdateMotion(this Entity g) {
			//Ground friction
			if(g.OnGround()) {
				g.Velocity.x *= 0.9;
				g.Velocity.y *= 0.9;
			}

			Point3 normal = g.Velocity.Normal;
			Point3 dest = g.Position;
			for (Point3 p = g.Position + normal; (g.Position - p).Magnitude < g.Velocity.Magnitude; p += normal) {
				if (g.World.voxels.Try(p) is Air) {
					dest = p;
				} else {
					break;
				}
			}
			g.Position = dest;
		}
		public static void UpdateMotionCollision(this Entity g, Func<Entity, bool> ignoreCollision) {
			Point3 normal = g.Velocity.Normal;
			Point3 dest = g.Position;
			for (Point3 p = g.Position + normal; (g.Position - p).Magnitude < g.Velocity.Magnitude; p += normal) {
				if (g.World.voxels.Try(p) is Air) {
					if(g.World.entities.Try(p).All(ignoreCollision)) {
						dest = p;
					}
				} else {
					break;
				}
			}
			g.Position = dest;
		}
	}
	class Player : Entity {
		public Point3 Velocity { get; set; }
		public Point3 Position { get; set; }
		public World World { get; set; }
		public HashSet<EntityAction> Actions { get; private set; }
		public HashSet<IItem> inventory { get; private set; }

		List<WorldEvent> history;	//All events that the player has witnessed
		List<WorldEvent> current;	//Events that the player is currently witnessing

		public int frameCounter = 0;

		public Player(World World, Point3 Position) {
			this.World = World;
			this.Position = Position;
			this.Velocity = new Point3(0, 0, 0);
			Actions = new HashSet<EntityAction>();
			inventory = new HashSet<IItem>();

			history = new List<WorldEvent>();
			current = new List<WorldEvent>();

			World.AddEntity(new Parachute(this));
		}
		public bool AllowUpdate() => Actions.Count > 0 && frameCounter == 0;
		public bool Active => true;
		public void OnRemoved() { }
		public void UpdateRealtime() {
			current.ForEach(e => e.ScreenTime--);
			if(frameCounter > 0)
				frameCounter--;
		}
		public void UpdateStep() {
			this.UpdateGravity();
			this.UpdateMotion();
			Actions.ToList().ForEach(a => a.Update());
			Actions.RemoveWhere(a => a.Done());
			foreach(var i in inventory) {
				i.Position = Position;
				i.Velocity = Velocity;
			}
			if(!this.OnGround())
				frameCounter = 20;

			current.RemoveAll(e => e.ScreenTime < 1);
		}

		public void Witness(WorldEvent e) {
			history.Add(e);
			current.Add(e);
		}

		public ColoredGlyph SymbolCenter => new ColoredString("@", Color.White, Color.Black)[0];
		public ColoredString Name => new ColoredString("Player", Color.White, Color.Black);
	}
	class ThrownItem : Entity {
		public Entity thrower { get; private set; }
		public IItem source { get; private set; }
		public World World { get => source.World; }
		public Point3 Position { get => source.Position; set => source.Position = value; }
		public Point3 Velocity { get => source.Velocity; set => source.Velocity = value; }
		public bool Active => flying && source.Active;
		private bool flying;
		private int tick = 0;
		public void OnRemoved() {
			if (source.Active) {
				source.World.AddEntity(source);
			}
		}
		public ThrownItem(Entity thrower, IItem source) {
			this.thrower = thrower;
			this.source = source;
			flying = true;
		}
		public ColoredGlyph SymbolCenter => tick % 20 < 10 ? source.SymbolCenter : source.SymbolCenter.Brighten(51);
		public ColoredString Name => tick % 20 < 10 ? source.Name : source.Name.Brighten(51);

		public void UpdateRealtime() {
			tick++;
			source.UpdateRealtime();
		}
		public void UpdateStep() {
			this.Info("UpdateStep()");
			source.UpdateGravity();
			source.UpdateMotionCollision(e => {
				if (e == thrower) {
					this.Info("Ignore collision with thrower");
					return true;
				} else {
					this.Info("Flying collision with object");
					flying = false;
					return false;
				}
			});
			if (this.OnGround()) {
				this.Info("Landed on ground");
				flying = false;
			}
		}
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
		public void UpdateRealtime() {

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
