﻿using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscendenceRL {
    public class Item {
        public ItemType type;
        public Weapon weapon;
        public Armor armor;
        public Shields shields;

        public Item(ItemType type) {
            this.type = type;
            //These fields are to remain null while the item is not installed and to be populated upon installation
            weapon = null;
            armor = null;
            shields = null;
        }
        public Weapon InstallWeapon() => weapon = new Weapon(this, type.weapon);
        public Armor InstallArmor() => armor = new Armor(this, type.armor);
        public Shields InstallShields() => shields = new Shields(this, type.shield);

        public void RemoveWeapon() => weapon = null;
        public void RemoveArmor() => armor = null;
        public void RemoveShields() => shields = null;
    }
    public interface Device {
        Item source { get; }
        void Update(IShip owner);
    }
    public class Weapon : Device {
        public Item source { get; private set; }
        public WeaponDesc desc;
        public SpaceObject target;
        public int fireTime;
        public bool firing;

        public Weapon(Item source, WeaponDesc desc) {
            this.source = source;
            this.desc = desc;
            this.fireTime = 0;
            firing = false;
        }
        public void Update(IShip owner) {
            double? targetAngle = null;
            if(target == null) {
                target = owner.World.entities.GetAll(p => (owner.Position - p).Magnitude < desc.range).OfType<SpaceObject>().FirstOrDefault(s => SShip.CanTarget(owner, s));
            } else {
                var angle = Helper.CalcFireAngle(target.Position - owner.Position, target.Velocity - owner.Velocity, desc.missileSpeed);
                if(desc.omnidirectional) {
                    Heading.AimLine(owner.World, owner.Position, angle);
                }
                targetAngle = angle;
            }

            if(fireTime > 0) {
                fireTime--;
            } else if(firing) {
                if(desc.omnidirectional && targetAngle != null) {
                    Fire(owner, targetAngle.Value);
                } else {
                    Fire(owner, owner.rotationDegrees * Math.PI / 180);
                }
                fireTime = desc.fireCooldown;
            }
            firing = false;
        }
        public void Fire(IShip source, double direction) {
            var shot = new Projectile(source, source.World,
                desc.effect.Glyph,
                source.Position + XY.Polar(direction),
                source.Velocity + XY.Polar(direction, desc.missileSpeed),
                desc.damageHP,
                desc.lifetime);
            source.World.AddEntity(shot);
        }
        public void SetFiring(bool firing = true) => this.firing = firing;

        //Use this if you want to override auto-aim
        public void SetFiring(bool firing = true, SpaceObject target = null) {
            this.firing = firing;
            this.target = target ?? this.target;
        }
    }
    public class Armor {
        public Item source { get; private set; }
        public ArmorDesc desc;
        public int hp;
        public Armor(Item source, ArmorDesc desc) {
            this.source = source;
            this.desc = desc;
            this.hp = desc.maxHP;
        }
        public void Update(IShip owner) {

        }
    }
    public class Shields : Device {
        public Item source { get; private set; }
        public ShieldDesc desc;
        public int hp;
        public int depletionTime;
        private int tick;
        public Shields(Item source, ShieldDesc desc) {
            this.source = source;
            this.desc = desc;
        } 
        public void Update(IShip owner) {
            if(depletionTime > 0) {
                depletionTime--;
            } else if(hp < desc.maxHP) {
                tick++;
                if(tick%desc.ticksPerHP == 0) {
                    hp++;
                }
            }
        }
        public void Absorb(int damage) {
            hp = Math.Max(0, hp - damage);
            if(hp == 0) {
                depletionTime = desc.depletionDelay;
            }
        }
    }
}
