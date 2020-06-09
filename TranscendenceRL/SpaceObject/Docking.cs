﻿using Common;

namespace TranscendenceRL {
    public class Docking {
        public Ship ship;
        public SpaceObject target;
        public bool docked;
        public Docking(Ship ship, SpaceObject target) {
            this.ship = ship;
            this.target = target;
        }
        public bool Update() {
            if(!docked) {
                docked = UpdateDocking();
                if(docked) {
                    return true;
                }
            } else {
                ship.Position = target.Position;
                ship.Velocity = target.Velocity;
            }
            return false;
        }
        public bool UpdateDocking() {
            double decel = ship.ShipClass.thrust / 2 * TranscendenceRL.TICKS_PER_SECOND;
            double stoppingTime = (ship.Velocity - target.Velocity).Magnitude / decel;

            double stoppingDistance = ship.Velocity.Magnitude * stoppingTime - (decel * stoppingTime * stoppingTime) / 2;
            var stoppingPoint = ship.Position;
            if (!ship.Velocity.IsZero) {
                stoppingPoint += ship.Velocity.Normal * stoppingDistance;
            }
            var offset = target.Position + (target.Velocity * stoppingTime) - stoppingPoint;

            if (offset.Magnitude > 0.25) {
                ship.Velocity += XY.Polar(offset.Angle, ship.ShipClass.thrust);
            } else if ((ship.Position - target.Position).Magnitude < 1) {
                ship.Velocity = target.Velocity;
                return true;
            }
            return false;
        }
    }
}