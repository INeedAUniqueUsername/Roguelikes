﻿using Common;
using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscendenceRL {
    public enum Rotating {
        None, CCW, CW
    }
    public static class SShip {
        public static bool CanTarget(this IShip owner, SpaceObject target) {

            { owner = (owner is AIShip s) ? s.Ship : owner; }
            { owner = (owner is PlayerShip s) ? s.Ship : owner; }
            { target = (target is AIShip s) ? s.Ship : target; }
            { target = (target is PlayerShip s) ? s.Ship : target; }

            return owner != target && owner.Sovereign.IsEnemy(target);
        }
    }
    public interface IShip : SpaceObject {
        ShipClass ShipClass { get; }
        double rotationDegrees { get; }
    }
    public class DeviceSystem {
        public List<Device> Installed;
        public List<Weapon> Weapons;
        public DeviceSystem() {
            Installed = new List<Device>();
            Weapons = new List<Weapon>();
        }
        public void Add(List<Device> Devices) {
            this.Installed.AddRange(Devices);
            UpdateDevices();
        }
        public void UpdateDevices() {
            Weapons = Installed.OfType<Weapon>().ToList();
        }
        public void Update(IShip owner) {
            Installed.ForEach(d => d.Update(owner));
        }
    }
    public class Docking {
        public Ship ship;
        public SpaceObject target;
        public bool done;
        public Docking(Ship ship, SpaceObject target) {
            this.ship = ship;
            this.target = target;
        }
        public bool Update() {
            if(!done) {
                done = UpdateDocking();
                if(done) {
                    return true;
                }
            }
            return false;
        }
        public bool UpdateDocking() {

            double decel = 10f / 30;
            double stoppingTime = ship.Velocity.Magnitude / decel;

            double stoppingDistance = ship.Velocity.Magnitude * stoppingTime - (decel * stoppingTime * stoppingTime) / 2;
            var stoppingPoint = ship.Position;
            if (!ship.Velocity.IsZero) {
                ship.Velocity -= XY.Polar(ship.Velocity.Angle, decel);
                stoppingPoint += ship.Velocity.Normal * stoppingDistance;
            }
            var offset = target.Position - stoppingPoint;

            if (offset.Magnitude > 0.25) {
                ship.Velocity += XY.Polar(offset.Angle, decel * 6);
            } else if ((ship.Position - target.Position).Magnitude < 1) {
                ship.Velocity = new XY(0, 0);
                return true;
            }
            return false;
        }
    }
    public class Ship : IShip {
        public string Name => ShipClass.name;
        public World World { get; private set; }
        public ShipClass ShipClass { get; private set; }
        public Sovereign Sovereign { get; private set; }
        public XY Position { get; set; }
        public XY Velocity { get; set; }
        public bool Active { get; private set; }
        public DeviceSystem Devices { get; private set; }
        private DamageSystem DamageSystem;
        public double rotationDegrees { get; private set; }
        public double stoppingRotation { get {
                var stoppingTime = 30 * Math.Abs(rotatingSpeed) / (ShipClass.rotationDecel);
                return rotationDegrees + rotatingSpeed * stoppingTime - ((ShipClass.rotationDecel / 30) * stoppingTime * stoppingTime) / 2;
        }}

        public bool thrusting;
        public Rotating rotating;
        public double rotatingSpeed;
        public bool decelerating;


        public Ship(World world, ShipClass shipClass, Sovereign Sovereign, XY Position) {
            this.World = world;
            this.ShipClass = shipClass;

            this.Sovereign = Sovereign;

            this.Position = Position;
            Velocity = new XY();

            this.Active = true;

            Devices = new DeviceSystem();
            Devices.Add(shipClass.devices.Generate(world.types));

            DamageSystem = shipClass.damageDesc.Create(this);
        }
        public void SetThrusting(bool thrusting = true) => this.thrusting = thrusting;
        public void SetRotating(Rotating rotating = Rotating.None) {
            this.rotating = rotating;
        }
        public void SetDecelerating(bool decelerating = true) => this.decelerating = decelerating;

        public void Damage(SpaceObject source, int hp) => DamageSystem.Damage(source, hp);

        public void Destroy() {
            World.AddEntity(new Wreck(this));
            Active = false;
        }

        public void Update() {
            UpdateControls();
            UpdateMotion();
            Devices.Update(this);
        }
        public void UpdateControls() {
            if (thrusting) {
                var rotationRads = rotationDegrees * Math.PI / 180;

                var exhaust = new EffectParticle(Position + XY.Polar(rotationRads, -1),
                    Velocity + XY.Polar(rotationRads, -ShipClass.thrust),
                    new ColoredGlyph('.', Color.Yellow, Color.Transparent),
                    4);
                World.AddEffect(exhaust);

                Velocity += XY.Polar(rotationRads, ShipClass.thrust);
                if (Velocity.Magnitude > ShipClass.maxSpeed) {
                    Velocity = Velocity.Normal * ShipClass.maxSpeed;
                }

                thrusting = false;
            }
            if (rotating != Rotating.None) {
                if (rotating == Rotating.CCW) {
                    /*
                    if (rotatingSpeed < 0) {
                        rotatingSpeed += Math.Min(Math.Abs(rotatingSpeed), ShipClass.rotationDecel);
                    }
                    */
                    rotatingSpeed += ShipClass.rotationAccel / 30;
                } else if (rotating == Rotating.CW) {
                    /*
                    if(rotatingSpeed > 0) {
                        rotatingSpeed -= Math.Min(Math.Abs(rotatingSpeed), ShipClass.rotationDecel);
                    }
                    */
                    rotatingSpeed -= ShipClass.rotationAccel / 30;
                }
                rotatingSpeed = Math.Min(Math.Abs(rotatingSpeed), ShipClass.rotationMaxSpeed) * Math.Sign(rotatingSpeed);
                rotating = Rotating.None;
            } else {
                rotatingSpeed -= Math.Min(Math.Abs(rotatingSpeed), ShipClass.rotationDecel / 30) * Math.Sign(rotatingSpeed);
            }
            rotationDegrees += rotatingSpeed;

            if (decelerating) {
                if (Velocity.Magnitude > 0.05) {
                    Velocity -= Velocity.Normal * Math.Min(Velocity.Magnitude, ShipClass.thrust / 2);
                } else {
                    Velocity = new XY();
                }
                decelerating = false;
            }
        }
        public void UpdateMotion() {
            Position += Velocity / 30;
        }
        public ColoredGlyph Tile => ShipClass.tile.Glyph;
    }
    public class AIShip : IShip {

        public static int ID = 0;
        public int Id = ID++;
        public string Name => Ship.Name;
        public World World => Ship.World;
        public ShipClass ShipClass => Ship.ShipClass;
        public Sovereign Sovereign => Ship.Sovereign;
        public XY Position => Ship.Position;
        public XY Velocity => Ship.Velocity;
        public double rotationDegrees => Ship.rotationDegrees;
        public DeviceSystem Devices => Ship.Devices;

        public Ship Ship;
        public Order controller;
        public Docking docking;

        public AIShip(Ship ship, Order controller) {
            this.Ship = ship;
            this.controller = controller;
            ship.World.AddEffect(new Heading(this));
        }
        public void SetThrusting(bool thrusting = true) => Ship.SetThrusting(thrusting);
        public void SetRotating(Rotating rotating = Rotating.None) => Ship.SetRotating(rotating);
        public void SetDecelerating(bool decelerating = true) => Ship.SetDecelerating(decelerating);
        public void Damage(SpaceObject source, int hp) => Ship.Damage(source, hp);
        public void Destroy() => Ship.Destroy();
        public void Update() {

            controller.Update();

            docking?.Update();

            Ship.UpdateControls();
            Ship.UpdateMotion();

            //We update the ship's devices as ourselves because they need to know who the exact owner is
            //In case someone other than us needs to know who we are through our devices
            Ship.Devices.Update(this);
        }
        public bool Active => Ship.Active;
        public ColoredGlyph Tile => Ship.Tile;
    }
    public class PlayerShip : IShip {
        public string Name => Ship.Name;
        public World World => Ship.World;
        public ShipClass ShipClass => Ship.ShipClass;
        public Sovereign Sovereign => Ship.Sovereign;
        public XY Position => Ship.Position;
        public XY Velocity => Ship.Velocity;
        public double rotationDegrees => Ship.rotationDegrees;

        public bool firingPrimary;
        public Ship Ship;
        public List<PlayerMessage> messages;
        private int selectedPrimary;

        public Docking docking;

        public HashSet<Entity> visible;
        public HashSet<Station> known;
        int ticks;

        public PlayerShip(Ship ship) {
            this.Ship = ship;
            ship.World.AddEffect(new Heading(this));
            messages = new List<PlayerMessage>();
            visible = new HashSet<Entity>();
            known = new HashSet<Station>();
            ticks = 0;
        }
        public void SetThrusting(bool thrusting = true) => Ship.SetThrusting(thrusting);
        public void SetRotating(Rotating rotating = Rotating.None) => Ship.SetRotating(rotating);
        public void SetDecelerating(bool decelerating = true) => Ship.SetDecelerating(decelerating);
        public void SetFiringPrimary(bool firingPrimary = true) => this.firingPrimary = firingPrimary;
        public void NextWeapon() {
            selectedPrimary++;
            if(selectedPrimary >= Ship.Devices.Weapons.Count) {
                selectedPrimary = 0;
            }
        }
        public void Damage(SpaceObject source, int hp) => Ship.Damage(source, hp);
        public void Destroy() => Ship.Destroy();
        public void Update() {
            messages.ForEach(m => m.Update());
            messages.RemoveAll(m => !m.Active);
            if(firingPrimary && selectedPrimary < Ship.Devices.Weapons.Count) {
                Ship.Devices.Weapons[selectedPrimary].SetFiring(true);
                firingPrimary = false;
            }

            ticks++;
            visible = new HashSet<Entity>(World.entities.GetAll(p => (Position - p).MaxCoord < 50));
            if (ticks%30 == 0) {
                foreach (var s in visible.OfType<Station>().Where(s => !known.Contains(s))) {
                    messages.Add(new PlayerMessage($"Discovered: {s.StationType.name}"));
                    known.Add(s);
                }
            }

            docking?.Update();

            Ship.UpdateControls();
            Ship.UpdateMotion();

            //We update the ship's devices as ourselves because they need to know who the exact owner is
            //In case someone other than us needs to know who we are through our devices
            Ship.Devices.Update(this);
        }
        public void AddMessage(PlayerMessage message) {
            var existing = messages.FirstOrDefault(m => m.message.String.Equals(message.message.String));
            if (existing != null) {
                existing.ticksRemaining = 150;
                existing.flashTicks = 15;
            } else {
                messages.Add(message);
            }
        }
        public bool Active => Ship.Active;
        public ColoredGlyph Tile => Ship.Tile;
    }
}
