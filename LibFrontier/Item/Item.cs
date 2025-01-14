﻿using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Helper = Common.Main;
using Newtonsoft.Json;
using System.Xml.Linq;
using LibGamer;
using static RogueFrontier.ItemType;

namespace RogueFrontier;
public class Item {
    public string name => type.name;
    public ItemType type;
    public Armor armor;
    public Engine engine;
    public Reactor reactor;
    public Service service;
    public Shield shield;
    public Solar solar;
    public Weapon weapon;
    public bool HasAtt(string att) => type.attributes.Contains(att);
    public Item() { }
    public Item(Item copy) {
        type = copy.type;
        weapon = copy.weapon?.Copy(this);
        armor = copy.armor?.Copy(this);
        shield = copy.shield?.Copy(this);
        reactor = copy.reactor?.Copy(this);
        solar = copy.solar?.Copy(this);
        service = copy.service?.Copy(this);
    }
    public Item(ItemType type) {
        this.type = type;
        weapon = type.Weapon?.GetWeapon(this);
        armor = type.Armor?.GetArmor(this);
        shield = type.Shield?.GetShield(this);
        reactor = type.Reactor?.GetReactor(this);
        solar = type.Solar?.GetSolar(this);
        service = type.Service?.GetService(this);
    }
    public T Get<T>() where T:class, Device{
        return (T)new Dictionary<Type, Device>() {
                [typeof(Armor)] = armor,
                [typeof(Engine)] = engine,
                [typeof(Reactor)] = reactor,
                [typeof(Service)]= service,
                [typeof(Shield)] = shield,
                [typeof(Solar)] = solar,
                [typeof(Weapon)] = weapon,
        }[typeof(T)];
    }
    public bool Get<T>(out T result) where T : class, Device => (result = Get<T>()) != null;
    public bool Has<T>() where T : class, Device => Get<T>() != null;
    public bool HasDevice() => (armor ?? engine ?? reactor??service??shield?? (Device)solar ?? weapon) != null;
    public IEnumerable<Device> GetDevices() => new List<Device> { armor, engine, reactor, shield, solar, weapon }.Where(d => d != null);
}
public interface Device {
    Item source { get; }
    void Update(double delta, IShip owner);
    int powerUse => -1;
    public bool IsEnabled(IShip owner) =>
        (owner as PlayerShip)?.energy.off.Contains(this) != false;
    public void OnOverload(PlayerShip owner) { }
    public void OnDisable() { }
}
/*
public class MultiItemAmmo : IAmmo {
    public int index;
    public List<IAmmo> missiles;
    public IAmmo current => missiles[index];
    public bool AllowFire => current.AllowFire;
    public MultiItemAmmo(List<IAmmo> missiles) {
        this.missiles = missiles;
    }
    public void Update(IShip source) => current.Update(source);
    public void Update(Station source) => current.Update(source);

    public void OnFire() => current.OnFire();
}
*/
public interface PowerSource {
    double energyDelta { get; set; }
    int maxOutput { get; }
}
public class CorrodeDesc {
    [Req] public double lifetime;
    [Req] public double damageRate = 0;
    [Opt] public bool lethal = false;
    [Opt] public double degradeFactor = 0;
    [Opt] public double fixedDegradeRate = 0;
    [Opt] public bool descend = false;
    public double silenceFactor;
    public ActiveObject source;
    public CorrodeDesc() { }
    public CorrodeDesc(XElement e) => e.Initialize(this);
    public CorrodeDesc(CorrodeDesc from, Projectile p) {
        this.lifetime = from.lifetime;
        this.damageRate = from.damageRate;
        this.lethal = from.lethal;
        this.degradeFactor = from.degradeFactor;
        this.fixedDegradeRate = from.fixedDegradeRate;
        this.descend = from.descend;
        silenceFactor = p.desc.silenceFactor;
        this.source = p.source;
    }
}

public class Corrode {
    public CorrodeDesc desc;
    public ActiveObject source;
    public double lifetime;

    public Corrode(CorrodeDesc desc, Projectile p) {
        this.desc = desc;
        this.source = p.source;
        this.lifetime = desc.lifetime;
    }
}
public class Armor : Device {
    [JsonProperty]
    public Item source { get; private set; }
    public ArmorDesc desc;
    public int hp;
    /// <summary>Titan is a temporary maxHP bonus that increases upon taking damage and decays over time</summary>
    public double titanHP;
    public double titanDuration;
    public double hpToRecover;
    public double recoveryHP;
    public double autoRegenHP;
    public double radioRegenHP;
    public double corrodeHP;
    public int killHP;
    public double damageDelay;
    public double stealth => desc.stealth == 0 ? 0 : desc.stealth * hp / desc.maxHP;
    public double lifetimeDamageAbsorbed;
    public int lastDamageTick = -1000;
    public int lastRegenTick = -1000;
    public HashSet<Corrode> corrode = [];
    public int powerUse { get; private set; }
    public bool allowRecovery;
    public bool hasRecovery => allowRecovery && Max(hpToRecover, Max(desc.freeRegenRate, killHP)) >= 1;
    public int maxHP => Max(0, desc.maxHP - (int)(desc.lifetimeDegrade * lifetimeDamageAbsorbed) + (int)titanHP);
    public bool canAbsorb => hp > 0 || Min(maxHP, desc.minAbsorb) > 0;
    public int apparentHP => Min(maxHP, Max(killHP, desc.minAbsorb));
    public double valueFactor => (0.5 * hp / desc.maxHP) + (0.5 * maxHP / desc.maxHP);
    public Armor() { }
    public Armor(Item source, ArmorDesc desc) {
        this.source = source;
        this.desc = desc;
        this.hp = desc.initialHP != -1 ? desc.initialHP : desc.maxHP;
        this.powerUse = desc.powerUse == -1 ? -1 : 0;
    }
    public Armor Copy(Item source) => desc.GetArmor(source);
    public void Repair(RepairArmor ra) {
        hp = Min(maxHP, hp + ra.repairHP);
    }
    public void Update(double delta, IShip owner) {
        UpdateCommon(delta, owner, owner.hull);
    }
    public void Update(double delta, Station st) =>
        UpdateCommon(delta, st, st.damageSystem);
    private void UpdateCommon(double delta, ActiveObject owner, HullSystem hull) {
        if (corrode.Any()) {
            var ownerSilence = owner switch {
                AIShip {ship:{silence:{ } s } } => s,
                PlayerShip { ship: { silence: { }s } } => s,
                _ => 0
            };
            var expired = new HashSet<Corrode>();
            //If the armor is down, then degrade it faster
            if (hp == 0) {
                if (hull.GetHP() == 0 && corrode.FirstOrDefault(d => d.desc.lethal) is { } kill) {
                    owner.Destroy(kill.source);
                } else {
                    if (corrode.Where(d => d.desc.descend).ToList() is { Count:>0 } descending &&
                        hull is LayeredArmor { layers: { } layers } &&
                        layers.Reverse<Armor>().Skip(layers.Count - layers.IndexOf(this)).FirstOrDefault(l => l.hp > 0) is { } next) {

                        corrode.ExceptWith(descending);
                        next.corrode.UnionWith(descending);
                    }
                    foreach (var d in corrode) {
                        var silenceMatch = FragmentDesc.GetSilenceMatch(d.desc.silenceFactor, ownerSilence);
                        lifetimeDamageAbsorbed += delta * 60 * (d.desc.damageRate * d.desc.degradeFactor + d.desc.fixedDegradeRate) * silenceMatch;
                        d.lifetime -= delta;
                        if (d.lifetime <= 0) {
                            expired.Add(d);
                        }
                    }
                }
                lastDamageTick = owner.world.tick;
            } else {
                var totalDegrade = 0d;
                foreach (var d in corrode) {
                    var silenceMatch = FragmentDesc.GetSilenceMatch(d.desc.silenceFactor, ownerSilence);

                    var degrade = delta * 60 * (d.desc.fixedDegradeRate) * silenceMatch;
                    totalDegrade += degrade;

                    corrodeHP += d.desc.damageRate * delta * 60 * silenceMatch;
                    d.lifetime -= delta;
                    if (d.lifetime <= 0) {
                        expired.Add(d);
                    }
                }
                if (corrodeHP >= 1) {
                    var deltaHP = Min(hp, (int)corrodeHP);
                    hp -= deltaHP;
                    lastDamageTick = owner.world.tick;

                    OnAbsorb(deltaHP);

                    corrodeHP = 0;
                }
                if (totalDegrade > 0) {
                    lifetimeDamageAbsorbed += totalDegrade;
                    UpdateHP();
                }
            }
            corrode.ExceptWith(expired);
        }
        if (titanHP > 0) {
            if (titanDuration > 0) {
                titanDuration = Max(0, titanDuration - delta);
            } else {
                titanHP = Max(0, titanHP - delta * desc.Titan.decay);
            }
        }
        if(desc.radioRegenRate > 0 && maxHP > desc.radioThreshold) {
			radioRegenHP += desc.radioRegenRate * delta * Constants.TICKS_PER_SECOND;
			while(radioRegenHP >= 1) {
				if(hp < maxHP) {
					lastRegenTick = owner.world.tick;
					hp++;
					radioRegenHP--;
                    lifetimeDamageAbsorbed += desc.radioDegradeRate;
				} else {
					radioRegenHP = 0;
				}
			}
		}
        allowRecovery = desc.powerUse == -1 || owner switch {
            PlayerShip { energy: { off: { } off } } => !off.Contains(this),
            _ => true
        };
        if (damageDelay > 0) {
            damageDelay -= delta * Constants.TICKS_PER_SECOND;
            return;
        }
        powerUse = desc.powerUse == -1 ? -1 : 0;
        if (!allowRecovery) {
            return;
        }
        if (hpToRecover >= 1) {
            recoveryHP += desc.recoveryRate * delta * Constants.TICKS_PER_SECOND;
            while (recoveryHP >= 1) {
                if (hp < maxHP) {
					powerUse = desc.powerUse;
					lastRegenTick = owner.world.tick;
					hp++;
                    recoveryHP--;
                    hpToRecover--;
                } else {
                    recoveryHP = 0;
                    hpToRecover = 0;
                }
            }
        }
        if (desc.freeRegenRate > 0) {
            autoRegenHP += desc.freeRegenRate * delta * Constants.TICKS_PER_SECOND;
            while (autoRegenHP >= 1) {
                if (hp < maxHP) {
					powerUse = desc.powerUse;
					lastRegenTick = owner.world.tick;
					hp++;
                    autoRegenHP--;
                } else {
                    autoRegenHP = 0;
                }
            }
        }
        if (hp > 0 && killHP < desc.killHP) {
            killHP = Min(maxHP, desc.killHP);
        }
        if (killHP > 0) {
            powerUse = desc.powerUse;
        }
    }
    private void UpdateHP() {
        hp = Min(hp, maxHP);
    }
    private void OnAbsorb(int absorbed, int damageDelay = 0) {
        lifetimeDamageAbsorbed += absorbed;
        if (desc.Titan is { } t) {
            titanHP = Min(desc.maxHP * t.factor, titanHP + absorbed * t.gain);
            titanDuration = t.duration;
        }
        hpToRecover += (absorbed * desc.recoveryFactor);
        this.damageDelay = Max(damageDelay, 30);
    }
    public void Damage(int amount) {
        if(hp == 0 || amount < 1) {
            return;
        }
        //Check if we have a kill threshold
        if (hp <= killHP) {
            if(amount < killHP) {
                //Remember this but take no damage
                OnAbsorb(amount);
            } else {
                //Otherwise, we fall
                hp = 0;
                OnAbsorb(killHP);
            }
            return;
        }
        var absorbed = Min(hp, amount);
        hp -= absorbed;
        OnAbsorb(absorbed);
        if (killHP > 0 && absorbed > killHP) {
            killHP = 0;
        }
    }
    public int Absorb(Projectile p) {
        if (p.damageLeft < 1)
            return 0;
        //If we have a minAbsorb, then we absorb damage even at 0 hp
        int damageWall = Min(maxHP, desc.minAbsorb);
        if(hp is 0) {
            //If we're down and have nothing to absorb, then give up
            if (damageWall == 0) {
                return 0;
            }
            //Otherwise, the projectile goes through the standard procedure
            //If we're below the drill threshold, then skip this armor
            if ((float)hp / desc.maxHP < p.desc.armorDrill) {
                return 0;
            }
            //If projectile has armor skip, then skip us now.
            if (p.armorSkip > 0) {
                p.armorSkip--;
                return 0;
            }
            //If we still have something to absorb, do it now
            if (damageWall > 0) {
                var deltaHP = Min(p.damageLeft, damageWall);
                p.damageLeft -= deltaHP;
                lifetimeDamageAbsorbed += deltaHP * 5;

                ApplyDecay();
                return 0;
            } else {
                throw new Exception("Impossible scenario encountered");
            }
        } else {
            
            
            //If we're below the drill threshold, then skip this armor
            if ((float)hp / desc.maxHP < p.desc.armorDrill) {
                return 0;
            }
            //If projectile has armor skip, then skip us now.
            if (p.armorSkip > 0) {
                p.armorSkip--;
                return 0;
            }
        }
        //Check if we have a kill threshold
        if (hp <= killHP) {
            if (p.damageLeft < killHP) {
                p.damageLeft = 0;
                var amount = p.damageLeft;
                p.hitBlocked = true;
                //Remember this but take no damage
                OnAbsorb(amount, p.desc.armorDisrupt);
                return amount;
            } else {
                p.damageLeft -= killHP;
                lastDamageTick = p.world.tick;
                //Otherwise, we fall
                hp = 0;
                OnAbsorb(killHP, p.desc.armorDisrupt);
                return killHP;
            }
        }
        var multiplier = p.desc.armorFactor;// + lifetimeDamageAbsorbed * desc.lifetimeDegrade;

        var totalDamage = p.damageLeft * multiplier;
        var absorbed = (int)Min(totalDamage, hp);
        if(desc.maxAbsorb > -1 && desc.maxAbsorb < absorbed) {
            absorbed = desc.maxAbsorb;
            //lifetimeDamageAbsorbed += (absorbed - desc.maxAbsorb) * 5;
        }
        if(desc.damageWall > -1 && desc.damageWall < absorbed) {
            absorbed = desc.damageWall;
			lifetimeDamageAbsorbed += (absorbed - desc.damageWall) * 5;
            p.damageLeft = 0;
		}
		hp = Max(0, hp - absorbed);
        OnAbsorb(absorbed, p.desc.armorDisrupt);
        if (killHP > 0 && absorbed >= killHP) {
            killHP = 0;
        }
        lastDamageTick = p.world.tick;
        ApplyDecay();
        double reflectChance = desc.reflectFactor * hp / desc.maxHP - p.desc.antiReflect;
        if (p.world.karma.NextDouble() < reflectChance) {
            p.hitReflected = true;
            return absorbed;
        }
        p.damageLeft = Max(0, p.damageLeft - (int)Ceiling(Max(absorbed, damageWall) / multiplier));
        return absorbed;

        void ApplyDecay() {
            if (p.desc.Corrode is { } d) {
                corrode.Add(new(d, p));
            }
        }
    }
}
public class Engine : Device {
    [JsonProperty]
    public Item source { get; private set; }
    public EngineDesc desc;
    public bool thrusting;
    public Engine() { }
    public Engine(Item source, EngineDesc desc) {
        this.source = source;
        this.desc = desc;
    }
    public Engine Copy(Item source) => desc.GetEngine(source);
    public void Update(double delta, IShip owner) {
        var rotationDeg = owner.rotationDeg;


        var ship = owner switch {
            PlayerShip { ship: { } s } => s,
            AIShip { ship: { } s } => s,
            _ => null
        };
        var sc = ship.shipClass;
        UpdateThrust();
        UpdateTurn();
        UpdateRotation();
        UpdateBrake();
        void UpdateThrust() {
            if (thrusting) {
                var rotationRads = rotationDeg * PI / 180;

                var exhaust = new EffectParticle(ship.position + XY.Polar(rotationRads, -1),
                    ship.velocity + XY.Polar(rotationRads, -sc.thrust),
                    new Tile(ABGR.Yellow, ABGR.Transparent, (char)249),
                    4);
                ship.world.AddEffect(exhaust);

                ship.velocity += XY.Polar(rotationRads, sc.thrust * delta * Constants.TICKS_PER_SECOND);
                if (ship.velocity.magnitude > ship.shipClass.maxSpeed) {
                    ship.velocity = ship.velocity.normal * sc.maxSpeed;
                }

                thrusting = false;
            }
        }
        void UpdateTurn() {
            if (ship.rotating != Rotating.None) {
                ref var rv = ref ship.rotatingVel;
                if (ship.rotating == Rotating.CCW) {
                    /*
                    if (rotatingSpeed < 0) {
                        rotatingSpeed += Math.Min(Math.Abs(rotatingSpeed), ShipClass.rotationDecel);
                    }
                    */
                    //Add decel if we're turning the other way
                    if (rv < 0) {
                        Decel();
                    }
                    rv += sc.rotationAccel * delta;
                } else if (ship.rotating == Rotating.CW) {
                    /*
                    if(rotatingSpeed > 0) {
                        rotatingSpeed -= Math.Min(Math.Abs(rotatingSpeed), ShipClass.rotationDecel);
                    }
                    */
                    //Add decel if we're turning the other way
                    if (rv > 0) {
                        Decel();
                    }
                    rv -= sc.rotationAccel * delta;
                }
                rv = Min(Abs(rv), sc.rotationMaxSpeed) * Sign(rv);
                ship.rotating = Rotating.None;
            } else {
                Decel();
            }
            void Decel() => ship.rotatingVel -= Min(Abs(ship.rotatingVel), sc.rotationDecel * delta) * Sign(ship.rotatingVel); ;
        }
        void UpdateRotation() => ship.rotationDeg += ship.rotatingVel;
        void UpdateBrake() {
            if (ship.decelerating) {
                if (ship.velocity.magnitude > 0.05) {
                    ship.velocity -= ship.velocity.normal * Min(ship.velocity.magnitude, sc.thrust * delta * Constants.TICKS_PER_SECOND / 2);
                } else {
                    ship.velocity = new XY();
                }
                ship.decelerating = false;
            }
        }
    }
}
public class Enhancer : Device {
    [JsonProperty]
    public Item source { get; set; }
    public EnhancerDesc desc;
    public int powerUse => desc.powerUse;
    public Enhancer() { }
    public Enhancer(Item source, EnhancerDesc desc) {
        this.source = source;
        this.desc = desc;
    }
    public Enhancer Copy(Item source) => desc.GetEnhancer(source);
    public void Update(double delta, IShip owner) {
    }
}
public class Launcher : Device {
    public LauncherDesc desc;
    public Weapon weapon;
    public int index;
    [JsonIgnore]
    public Item source => weapon.source;
    [JsonIgnore]
    public LaunchDesc fragmentDesc => desc.missiles[index];
    [JsonIgnore]
    int Device.powerUse => ((Device)weapon).powerUse;
    [JsonIgnore]
    public Capacitor capacitor => weapon.capacitor;
    [JsonIgnore]
    public IAiming aiming => weapon.aiming;
    [JsonIgnore]
    public IAmmo ammo => weapon.ammo;
    [JsonIgnore]
    public double delay => weapon.delay;
    [JsonIgnore]
    public bool firing => weapon.firing;
    [JsonIgnore]
    public int repeatsLeft => weapon.repeatsLeft;
    public Launcher() { }
    public Launcher(Item source, LauncherDesc desc) {
        this.weapon = desc.GetWeapon(source);
        this.desc = desc;
    }
    public Launcher Copy(Item source) => desc.GetLauncher(source);
    public void SetMissile(int index) {
        this.index = index;
        var l = desc.missiles[index];
        weapon.ammo = new ItemAmmo(l.ammoType);
        weapon.desc.projectile = l.shot;
    }
    public string GetReadoutName() => weapon.GetReadoutName();
    public Tile[] GetBar(int BAR) => weapon.GetBar(BAR);
    public void Update(double delta, Station owner) => weapon.Update(delta, owner);
    public void Update(double delta, IShip owner) => weapon.Update(delta, owner);
    public void OnDisable() => weapon.OnDisable();
    public bool RangeCheck(ActiveObject user, ActiveObject target) => weapon.RangeCheck(user, target);
    public bool AllowFire => weapon.AllowFire;
    public bool ReadyToFire => weapon.ReadyToFire;
    public void Fire(ActiveObject owner, double direction) => weapon.Fire(owner, direction);
    public ActiveObject target => weapon.target;
    public void OverrideTarget(ActiveObject target) => weapon.SetTarget(target);
    public void SetFiring(bool firing = true) => weapon.SetFiring(firing);
    public void SetFiring(bool firing = true, ActiveObject target = null) => weapon.SetFiring(firing, target);
}
public class Reactor : Device, PowerSource {
    [JsonProperty]
    public Item source { get; set; }
    public ReactorDesc desc;
    public double energy;
    [JsonProperty]
    public double energyDelta { get; set; }
    public double combatEnergyDelta;
    public int rechargeDelay;
    //public int maxOutput => energy > 0 ? (int)Math.Max(0, desc.maxOutput - Math.Floor(desc.lifetimeDegrade * lifetimeOutput)) : 0;
    public int maxOutput => energy > 0 ? desc.maxOutput : 0;
    public double lifetimeOutput;
    public double efficiency => desc.efficiency - 0.01 * desc.lifetimeDegrade * Max(0, lifetimeOutput - desc.degradeDelay);
    public Reactor() { }
    public Reactor(Item source, ReactorDesc desc) {
        this.source = source;
        this.desc = desc;
        if (desc.startFull) {
            energy = desc.capacity;
        }
        energyDelta = 0;
    }
    public Reactor Copy(Item source) => desc.GetReactor(source);
    public void Update(double delta, IShip owner) {
        var prevEnergy = energy;
        var effectiveDelta = energyDelta;
        if(energyDelta < 0) {
            effectiveDelta = effectiveDelta - combatEnergyDelta + combatEnergyDelta / desc.combatFactor;
            combatEnergyDelta = 0;
            //energyDelta = -Math.Max(-energyDelta, desc.minOutput);
            var efficiencyAdj = (efficiency - 1) * (-effectiveDelta / desc.maxOutput);
            effectiveDelta /= 1 + efficiencyAdj;
        }
        energy = Clamp(energy + effectiveDelta * delta, 0, desc.capacity);
        lifetimeOutput += Max(0, prevEnergy - energy);
    }
}
public class Service : Device {
    [JsonProperty]
    public Item source { get; private set; }
    public ServiceDesc desc;
    public int ticks;
    [JsonProperty]
    public int powerUse { get; private set; }
    public Service() { }
    public Service(Item source, ServiceDesc desc) {
        this.source = source;
        this.desc = desc;
        powerUse = 0;
    }
    public Service Copy(Item source) => desc.GetService(source);
    public void Update(double delta, IShip owner) {
        ticks++;
        if (ticks % desc.interval == 0) {
            var powerUse = 0;
            switch (desc.type) {
                case ServiceType.missileJack: {
                        //May not work in Arena mode if we assume control
                        //bc weapon locks are focused on the old AI ship
                        var missile = owner.world.entities.all
                            .OfType<Projectile>()
                            .FirstOrDefault(
                                p => (owner.position - p.position).magnitude < 24
                                  && p.maneuver is { desc.maneuver: > 0 }
                                  && Equals(p.maneuver.target, owner)
                                );
                        if (missile != null) {
                            missile.maneuver.target = missile.source;
                            missile.source = owner;
                            var offset = (missile.position - owner.position);
                            var dist = offset.magnitude;
                            var inc = offset.normal;
                            for (var i = 0; i < dist; i++) {
                                var p = owner.position + inc * i;
                                owner.world.AddEffect(new EffectParticle(p, new Tile(ABGR.Orange, ABGR.Transparent, '-'), 10));
                            }
                            powerUse = desc.powerUse;
                        }
                        break;
                    }
                case ServiceType.armorRepair: {
                        break;
                    }
                case ServiceType.grind:
                    if (owner is PlayerShip player) {
                        powerUse = this.powerUse + (player.energy.totalOutputMax - player.energy.totalOutputUsed);
                    }
                    break;
            }
            this.powerUse = powerUse;
        }
    }
    void Device.OnOverload(PlayerShip owner) {
        powerUse = owner.energy.totalOutputLeft;
    }
}
public class Shield : Device {
    [JsonProperty]
    public Item source { get; private set; }
    public ShieldDesc desc;
    public int hp;
    public double regenHP;
    public double delay;
    public double absorbFactor => desc.absorbFactor;
    public int maxAbsorb => hp;
    public int lifetimeDamageAbsorbed;
    public int stealth => desc.stealth == 0 ? 0 :
        delay > 0 ? 0 :
        desc.stealth * hp / desc.maxHP;
    public int powerUse => hp < desc.maxHP ? desc.powerUse : desc.idlePowerUse;
    public Shield() { }
    public Shield(Item source, ShieldDesc desc) {
        this.source = source;
        this.desc = desc;
    }
    public Shield Copy(Item source) => desc.GetShield(source);
    public void OnDisable() => Deplete();
    public void Deplete() {
        hp = 0;
        regenHP = 0;
        delay = desc.depletionDelay;
    }
    public void Update(double delta, IShip owner) {
        if (delay > 0) {
            delay -= delta * Constants.TICKS_PER_SECOND;
        } else {
            regenHP += desc.regen * delta * Constants.TICKS_PER_SECOND;
            while (regenHP >= 1) {
                if (hp < desc.maxHP) {
                    hp++;
                    regenHP--;
                } else {
                    regenHP = 0;
                }
            }
            /*
            absorbRegenHP += desc.absorbRegen;
            while(absorbRegenHP >= 1) {
                if(absorbHP < desc.absorbMaxHP) {
                    absorbHP++;
                    absorbRegenHP--;
                } else {
                    absorbRegenHP = 0;
                }
            }
            */
        }
    }
    public void Absorb(Projectile p) {
        var multiplier = p.desc.shieldFactor;
        var absorbed = (int)Clamp(p.damageLeft * (1 - p.desc.shieldDrill) * absorbFactor * multiplier, 0, maxAbsorb);
        if (absorbed > 0) {
            hp -= absorbed;
            lifetimeDamageAbsorbed += absorbed;
            delay = (hp == 0 ? desc.depletionDelay : desc.damageDelay);

            double reflectChance = desc.reflectFactor * hp / desc.maxHP - p.desc.antiReflect;
            if (p.world.karma.NextDouble() < reflectChance) {
                p.hitReflected = true;
                return;
            }
            p.damageLeft -= (int)Ceiling(absorbed / multiplier);
        }
    }
}
public class Solar : Device, PowerSource {
    [JsonProperty]
    public Item source { get; private set; }
    public SolarDesc desc;
    public int durability;
    [JsonProperty]
    public int maxOutput { get; private set; }
    [JsonProperty]
    public double energyDelta { get; set; }
    public Solar() { }
    public Solar(Item source, SolarDesc desc) {
        this.source = source;
        this.desc = desc;
        durability = desc.durability;
    }
    public Solar Copy(Item source) => desc.GetSolar(source);
    public void Update(double delta, IShip owner) {
        void Update() {
            var t = owner.world.backdrop.starlight.GetBackgroundFixed(owner.position);
            var b = ABGR.GetLightness(t);
            maxOutput = (int)(b * desc.maxOutput);
        }
        switch (durability) {
            case -1:
                Update();
                break;
            case 0:
                break;
            case 1:
                durability = 0;
                maxOutput = 0;
                if (owner is PlayerShip ps) {
                    ps.AddMessage(new Message($"{source.name} has stopped functioning"));
                }
                break;
            case > 1:
                durability = (int)Max(1, durability + energyDelta * delta);
                Update();
                break;
            default: throw new Exception($"Invalid durability value {durability}");
        }
    }
}
public class Weapon : Device, Ob<Projectile.OnHitActive> {
    [JsonProperty]
    public Item source { get; private set; }
    public WeaponDesc desc;
    [JsonIgnore]
    public int powerUse => (firing || delay > 0 || capacitor?.full == false) ? desc.powerUse : 0;
    public IAiming aiming;
    public Targeting targeting;
    public Capacitor capacitor;
    public IAmmo ammo;
    public FragmentMod mod = FragmentMod.EMPTY;
    public FragmentDesc projectileDesc;
    public bool structural;
    public double delay;
    public bool firing;
    public int repeatsLeft;
    public double angle;
    public bool blind;
    public double criticalFactor = 0.0;
    public XY offset=new(0,0);
    public int lastFireTick;
    public double timeSinceLastFire;
    public int totalTimesFired;
    public record OnFire(Weapon w, List<Projectile> p);
    public Vi<OnFire> onFire=new();
    public double valueFactor =>
        ammo is ChargeAmmo ca ?
            (double)ca.charges / desc.initialCharges :
            1.0;

    public Weapon() { }
    public Weapon(Item source, WeaponDesc desc, IAiming aiming = null) {
        this.source = source;
        this.aiming = aiming;
        SetWeaponDesc(desc);
    }
    public bool IsInRange(XY offset) => offset.magnitude2 < projectileDesc.range2;
    public void SetWeaponDesc(WeaponDesc desc) {
        this.desc = desc;

        capacitor =
            desc.capacitor != null ?
                new(desc.capacitor) :
            null;
        ammo =
            desc.initialCharges > -1 ?
                new ChargeAmmo(desc.initialCharges) :
            desc.ammoType != null ?
                new ItemAmmo(desc.ammoType) :
            null;
        aiming ??=
            desc.projectile.omnidirectional || desc.omnidirectional ?
                new Omnidirectional() :
            desc.sweep > 0 ?
                new Swivel(desc.sweep) :
            desc.leftRange + desc.rightRange > 0 ?
                new Swivel(desc.leftRange, desc.rightRange) :
            null;
        targeting =
            desc.projectile.multiTarget ?
                new(true) :
            desc.projectile.acquireTarget ?
                new(false) :
            aiming != null ?
                new(false) :
            desc.projectile.guidanceDesc is { } ?
                new(false) :
            null;
        UpdateProjectileDesc();
        structural = desc.structural;
    }
    public Weapon Copy(Item source) => desc.GetWeapon(source);
    public string GetReadoutName() {
        string name = source.type.name;
        return ammo switch {
            ChargeAmmo { charges: {}charges } => $"{charges, 6} {name}",
            ItemAmmo { count: {} count } => $"{count, 6} {name}",
            _ => $"     * {name}"
        };
    }
    public Tile[] GetBar(int BAR) {
        if (ammo?.AllowFire == false) {
            return Tile.Arr(new(' ', BAR), ABGR.Transparent, ABGR.Black);
        }
        var fireBar = (int)(BAR * (double)(desc.fireCooldown - delay) / desc.fireCooldown);
        Tile[] bar;
        if (capacitor != null && capacitor.desc.minChargeToFire > 0) {
            var chargeBar = (int)(BAR * Min(1, capacitor.charge / capacitor.desc.minChargeToFire));
            bar = [
                ..Tile.Arr(new('>', chargeBar), ABGR.Gray, ABGR.Black),
                ..Tile.Arr(new(' ', BAR - chargeBar), ABGR.Transparent, ABGR.Black)];
        } else {
            bar = Tile.Arr(new('>', BAR), ABGR.Gray, ABGR.Black);
        }
        var l = Min(fireBar, bar.Length);

		Array.Copy(Enumerable.ToArray(from t in bar[..l] select t with { Foreground = ABGR.White }), bar, l);

        if (capacitor != null) {
            var n = BAR * capacitor.charge / capacitor.desc.maxCharge;
            var c = bar.Take((int)n + 1).ToArray();
            Array.Copy(Enumerable.ToArray(from t in c select t with { Foreground = ABGR.Blend(t.Foreground, ABGR.SetA(ABGR.Cyan, 128)) }), bar, c.Length);
        }
        return bar;
    }
    public void PeriodicUpdateProjectileDesc(ActiveObject owner, int interval) {
        if(owner.world.tick % interval == 0) {
            UpdateProjectileDesc();
        }
    }
    public void UpdateProjectileDesc() {
        projectileDesc = FragmentMod.Sum(capacitor?.mod, mod) * desc.projectile;
    }
    public void Update(double delta, Station owner) {
        if (!blind) {
            targeting?.Update(owner, this);
            aiming?.Update(owner, this);
        }
        capacitor?.Update(this);
        timeSinceLastFire += delta;
        if (delay > 0) {
            delay -= delta * Constants.TICKS_PER_SECOND;
            PeriodicUpdateProjectileDesc(owner, 30);

            goto Done;
        }

        double? direction = aiming?.GetFireAngle();
        var hasAimAngle = direction != null;

        firing = direction.HasValue;

        bool beginRepeat = true;
        bool endRepeat = false;
        if (repeatsLeft > 0) {
            repeatsLeft--;
            firing = true;
            beginRepeat = false;
            endRepeat = repeatsLeft == 0;
        } else if (desc.autoFire) {
            bool CheckProjectile() {
                if (desc.targetProjectile && !blind && Targeting.AcquireMissile(owner, this, s => SStation.IsEnemy(owner, s)) is Projectile target) {
                    direction = Omnidirectional.GetFireAngle(owner, target, this);
                    return true;
                }
                return false;
            }
            bool CheckSpray() {
                direction = desc.spray ? aiming switch {
                    Omnidirectional => new Random().NextDouble() * 2 * PI,
                    Swivel s => angle + new Random().NextDouble() * (s.leftRange + s.rightRange) - s.leftRange,
                    _ => angle
                } : direction;
                return desc.spray;
            }
            if (!(firing = CheckProjectile() || CheckSpray() || hasAimAngle)) {
                goto Cancel;
            }
        } else if (!firing) {
            goto Cancel;
        }
        if (direction == null) {
            goto Cancel;
        }

        goto LineCheckDone;
        var d = XY.Polar(direction.Value);
        var p = owner.position;
        for (int i = 0; i < projectileDesc.range; i++) {
            p += d;
            foreach (var other in owner.world.entities[p].Select(s => s is ISegment { parent: { }p } ? p : s).Distinct()) {
                switch (other) {
                    case ActiveObject a when (targeting?.HasTarget(a) == true) || owner.CanTarget(a):
                        goto LineCheckDone;
                    case Station s when s == owner:
                    case PlayerShip ps when !owner.CanTarget(ps):
                    case AIShip ai when owner.guards.Contains(ai) || !owner.CanTarget(ai):
                    case Wreck:
                    case Projectile:
                        continue;
                    default:
                        firing = false;
                        goto LineCheckDone;
                }
            }
        }
    LineCheckDone:
        ammo?.Update(owner);
        if (!firing || !(capacitor?.AllowFire ?? true) || !(ammo?.AllowFire ?? true)) {
            goto Cancel;
        }
        UpdateProjectileDesc();
        delay =
            //No repeats
            desc.repeat == 0 ?
                desc.fireCooldown :
            //Begin
            beginRepeat ?
                desc.repeatDelay :
            //End
            endRepeat ?
                desc.fireCooldown :
            //Middle
                desc.repeatDelay;
        if (beginRepeat) {
            repeatsLeft = desc.repeat;
        }
        Fire(owner, direction.Value);
        goto Done;

    Cancel:
        PeriodicUpdateProjectileDesc(owner, 15);
        repeatsLeft = 0;

    Done:
        firing = false;
        blind = false;
    }
    public void Update(double delta, IShip owner) {
        if (!blind) {
            targeting?.Update(owner, this);
            aiming?.Update(owner, this);
        }
        capacitor?.Update(this);
        timeSinceLastFire += delta;
        if (delay > 0) {
            delay -= delta * Constants.TICKS_PER_SECOND;
            PeriodicUpdateProjectileDesc(owner, 30);
            goto Done;
        }
        double direction = owner.rotationRad + angle;
        var hasAimAngle = false;
        if (aiming?.GetFireAngle() is { } aimAngle) {
            hasAimAngle = true;
            direction = aimAngle;
        }
        bool beginRepeat = true;
        bool endRepeat = false;
        if (repeatsLeft > 0) {
            repeatsLeft--;
            firing = true;
            beginRepeat = false;
            endRepeat = repeatsLeft == 0;
        } else if (desc.autoFire) {
            bool CheckProjectile() {
                if (desc.targetProjectile && !blind && Targeting.AcquireMissile(owner, this, s => s != null && SShip.IsEnemy(owner, s)) is { } target) {
                    direction = Omnidirectional.GetFireAngle(owner, target, this);
                    return true;
                }
                return false;
            }
            bool CheckSpray() {
                direction = desc.spray ? new Random().NextDouble() * 2 * PI : direction;
                return desc.spray;
            }
            if (!(firing = CheckProjectile() || CheckSpray() || hasAimAngle)) {
                goto Cancel;
            }
        }
        ammo?.Update(owner);
        if (!firing || !(capacitor?.AllowFire ?? true) || !(ammo?.AllowFire ?? true)) {
            goto Cancel;
        }
        UpdateProjectileDesc();
        delay =
            //No repeats
            desc.repeat == 0 ?
                desc.fireCooldown :
            //Begin
            beginRepeat ?
                desc.repeatDelay :
            //End
            endRepeat ?
                desc.fireCooldown :
                //Middle
                desc.repeatDelay;
        if (beginRepeat) {
            repeatsLeft = desc.repeat;
        }
        if(desc.failureRate > 0 && owner.world.karma.NextDouble() < desc.failureRate) {
            repeatsLeft = 0;
            delay = desc.fireCooldown * 3;
            if(owner is PlayerShip pl) {
                pl.AddMessage(new Message("Weapon failed!"));
            }
        }
        Fire(owner, direction);
        //Apply on next tick (create a delta-momentum variable)
        if (desc.recoil > 0) {
            owner.velocity += XY.Polar(direction + PI, desc.recoil);
        }
        goto Done;
    Cancel:
        PeriodicUpdateProjectileDesc(owner, 15);
        repeatsLeft = 0;
    Done:
        firing = false;
        blind = false;
    }
    public void OnDisable() {
        //delay = desc.fireCooldown;
        //capacitor?.Clear();
        //targeting?.ClearTarget();
    }
    public bool RangeCheck(ActiveObject user, ActiveObject target) =>
        (user.position - target.position).magnitude < projectileDesc.range;
    public bool AllowFire => ammo?.AllowFire ?? true;
    public bool ReadyToFire => delay == 0 && (capacitor?.AllowFire ?? true) && (ammo?.AllowFire ?? true);
    public List<Projectile> CreateProjectiles(ActiveObject owner, List<ActiveObject> targets, double direction, bool sound = true) {
        HashSet<Entity> exclude = new() { null };
        if (!projectileDesc.hitSource) {
            exclude.Add(owner);
        }
        var projectiles = projectileDesc.CreateProjectiles(owner, targets, direction, offset, exclude);
        var criticalChance = 1.0 / (1 + criticalFactor);
        foreach (var p in projectiles) {
            if (owner.world.karma.NextDouble() > criticalChance) {
                p.damageLeft *= 3;
            }
        }
        projectiles.ForEach(p => p.onHitActive += this);
        exclude.UnionWith(projectiles);
        exclude.UnionWith(owner.world.entities.all.OfType<ActiveObject>().Where(a => !owner.CanTarget(a)));
        switch (owner) {
            case PlayerShip p:
                exclude.UnionWith(p.avoidHit);
                AllowWeaponTargets(p.devices.Weapon);
                p.onWeaponFire.Observe(new(p, this, projectiles, sound));
                break;
            case AIShip ai:
                exclude.UnionWith(ai.avoidHit);
                AllowWeaponTargets(ai.devices.Weapon);
                ai.onWeaponFire.Observe(new(ai, this, projectiles));
                break;
            case Station st:
                exclude.UnionWith(st.guards);
                AllowWeaponTargets(st.weapons);
                st.onWeaponFire.Observe(new(st, this, projectiles));
                break;
            case null:
                return projectiles;
        }
        if (targets != null) {
            exclude.ExceptWith(targets);
        }
        return projectiles;
        void AllowWeaponTargets(IEnumerable<Weapon> weapons) =>
            exclude.ExceptWith(weapons.Select(w => w.targeting).Where(a => a != null).SelectMany(w => w.GetMultiTarget()));
    }
    public void Fire(ActiveObject owner, double direction, List<Projectile> result = null) {
        var targets = targeting?.GetMultiTarget().ToList();
        var projectiles = CreateProjectiles(owner, targets, direction);
        projectiles.ForEach(owner.world.AddEntity);
        result?.AddRange(projectiles);


        targeting?.OnFire(this);
        ammo?.OnFire();
        capacitor?.OnFire();
        onFire.Observe(new(this, projectiles));
        timeSinceLastFire = 0;
        lastFireTick = owner.world.tick;
        totalTimesFired++;
    }
    public record OnHitActive(Weapon w, Projectile p, ActiveObject hit);
    public Vi<OnHitActive> onHitActive = new();
    public void Observe(Projectile.OnHitActive ev) {
        (var projectile, var hit) = ev;
        projectile.onHitActive -= this;
        onHitActive.Observe(new(this, projectile, hit));
        if (projectile.hitHull) {
            if (projectileDesc.tracker != 0) {
                switch (projectile.source) {
                    case PlayerShip pl:
                        HandlePlayer(pl);
                        break;
                    case AIShip {behavior: Wingmate w }:
                        HandlePlayer(w.player);
                        break;
                }
                void HandlePlayer(PlayerShip pl) {
                    var time = projectileDesc.tracker;
                    if (pl.tracking.TryGetValue(hit, out var t)) {
                        time = Max(t, time);
                    }
                    pl.tracking[hit] = time;
                }
            }
            if (projectileDesc.hook) {
                hit.world.AddEntity(new Hook(hit, projectile.source));
            }
            if (projectileDesc.lightning) {
                //delay = 5;
                hit.world.AddEntity(new LightningRod(hit, this, projectile));
            }
        }
    }
    public ActiveObject target => targeting?.target;
    public void SetTarget(ActiveObject target) =>
        targeting?.SetTarget(target);
    public void SetFiring(bool firing = true) => this.firing = firing;
    //Use this if you want to override auto-aim
    public void SetFiring(bool firing = true, ActiveObject forceTarget = null) {
        this.firing = firing;
        if (forceTarget != null) {
            targeting?.UpdateTarget(forceTarget);
        }
    }
}
public class Capacitor {
    public CapacitorDesc desc;
    public double charge;
    public bool full => charge == desc.maxCharge;
    public Capacitor(CapacitorDesc desc) {
        this.desc = desc;
    }
    public void CheckFire(ref bool firing) => firing = firing && AllowFire;
    public bool AllowFire => desc.minChargeToFire <= charge;
    public void Update(Weapon w) =>
        charge = Min(desc.maxCharge, charge +
            ((desc.requireReady && w.delay > 0) ? 0: desc.rechargePerTick));
    public FragmentMod mod => new() {
        damageHP =      new(inc: (int)(charge * desc.bonusDamagePerCharge)),
        missileSpeed =  new(inc: (int)(charge * desc.bonusSpeedPerCharge)),
        lifetime =      new(inc: (int)(charge * desc.bonusLifetimePerCharge))
    };
    /*
    public FragmentDesc Modify(FragmentDesc fd) =>
        fd with {
            damageHP = new DiceInc(fd.damageHP, (int)(desc.bonusDamagePerCharge * charge)),
            missileSpeed = fd.missileSpeed + (int)(desc.bonusSpeedPerCharge * charge),
            lifetime = fd.lifetime + (int)(desc.bonusLifetimePerCharge * charge)
        };
    public void Modify(ref FragmentDesc fd) =>
        fd = fd with {
            damageHP = new DiceInc(fd.damageHP, (int)(desc.bonusDamagePerCharge * charge)),
            missileSpeed = fd.missileSpeed + (int)(desc.bonusSpeedPerCharge * charge),
            lifetime = fd.lifetime + (int)(desc.bonusLifetimePerCharge * charge)
        };
    */
    public void OnFire() =>
        charge = Max(0, charge - desc.dischargeOnFire);
    public void Clear() => charge = 0;
}
public class Targeting : IDestroyedListener {
    public bool cycleTargets;
    private int ticks;
    public void Observe(IDestroyedListener.Destroyed ev) {
        var (s, d) = ev;
        targets.Remove(s);
        tracker.Adjust(s);
        ticks = 0;
    }
    public IEnumerable<ActiveObject> GetMultiTarget() {
        if (cycleTargets) {
            //Put the current index first
            foreach(var t in tracker.GetAllNext()) {
                yield return t;
            }
        } else {
            yield return target;
        }
    }
    public List<ActiveObject> targets => tracker.list;
    public ListTracker<ActiveObject> tracker;
    public ActiveObject target {
        get => tracker.item;
        set {
            tracker.list.Clear();
            tracker.list.Add(value);
            tracker.Reset();
        }
    }
    public Targeting(bool cycleTargets) {
        this.cycleTargets = cycleTargets;
        tracker = new(new List<ActiveObject>());
    }
    public void Update(ActiveObject owner, Weapon weapon, Func<ActiveObject, bool> filter) {
        if (ticks++ % 30 != 0) {
            return;
        }

        targets.RemoveAll(t => !(t?.active == true
            && owner.CanSee(t)
            && weapon.IsInRange(owner.position - t.position)
            ));
        
        if(ticks%60 == 0) {
            targets.AddRange(AcquireTargets(owner, weapon, filter));
        }
        tracker.Adjust(target);
        if (targets.Any()) {
            return;
        }
        var currentTargets = SSpaceObject.GetWeaponTargets(owner);
        targets.AddRange(AcquireTargets(owner, weapon, filter));
        tracker.Reset();
        tracker.Skip(currentTargets);
        foreach (var t in targets) {
            ((IDestroyedListener)this).Register(t);
        }
    }
    public void OnFire(Weapon weapon) {
        if (cycleTargets) {
            tracker++;
            return;
        } 
        /*
        if(weapon.aiming is { }a && a.GetFireAngle() == null) {
            tracker++;
        }
        */
    }
    public void Update(Station owner, Weapon weapon) =>
        Update(owner, weapon, other => owner.CanSee(other) && SStation.IsEnemy(owner, other));
    public void Update(IShip owner, Weapon weapon) =>
        Update(owner, weapon, other => owner.CanSee(other) && SShip.IsEnemy(owner, other));
    public void ClearTarget() => targets.Clear();
    public bool HasTarget(ActiveObject o) => targets.Contains(o);
    public void SetTarget(ActiveObject target) {
        ClearTarget();
        targets.Add(target);
    }
    public void UpdateTarget(ActiveObject target) {
        targets.Insert(0, target);
    }
    public static ActiveObject AcquireTarget(ActiveObject owner, Weapon weapon, Func<ActiveObject, bool> filter) {
        return owner.world.entities.FilterKey(p =>
            weapon.IsInRange(owner.position - p))
            .OfType<ActiveObject>()
            .FirstOrDefault(filter);
    }
    public static IEnumerable<ActiveObject> AcquireTargets(ActiveObject owner, Weapon weapon, Func<ActiveObject, bool> filter) =>
        owner.world.entities.FilterKey(p =>
            weapon.IsInRange(owner.position - p))
            .OfType<ActiveObject>()
            .Where(filter);
    public static Projectile AcquireMissile(ActiveObject owner, Weapon weapon, Func<ActiveObject, bool> filter) =>
        owner.world.entities.all
            .OfType<Projectile>()
            .Where(p => (owner.position - p.position).magnitude2 < weapon.projectileDesc.range2)
            .Where(p => filter(p.source))
            .OrderBy(p => (owner.position - p.position).Dot(p.velocity))
            //.OrderBy(p => (owner.Position - p.Position).Magnitude2)
            .FirstOrDefault();
}
public interface IAiming {
    void Update(Station owner, Weapon weapon);
    void Update(IShip owner, Weapon weapon);
    double? GetFireAngle() => null;
}
public class Omnidirectional : IAiming {
    double? direction;
    public Omnidirectional() {
    }
    public static double GetFireAngle(MovingObject owner, MovingObject target, Weapon w) =>
        Helper.CalcFireAngle(target.position - (owner.position + w.offset),
            target.velocity - owner.velocity,
            w.projectileDesc.missileSpeed, out var _);
    public void UpdateDirection(ActiveObject owner, Weapon weapon) {
        var t = weapon.targeting.target;
        if (t != null) {
            direction = GetFireAngle(owner, t, weapon);
            Heading.AimLine(owner.world, owner.position + weapon.offset, direction.Value);
            //Heading.Crosshair(owner.world, target.position);
        } else {
            direction = null;
        }
    }
    public void Update(Station owner, Weapon weapon) {
        UpdateDirection(owner, weapon);
        
    }
    public void Update(IShip owner, Weapon weapon) {
        UpdateDirection(owner, weapon);
    }
    public double? GetFireAngle() => direction;
}
public class Swivel : IAiming {
    private double facing;
    public readonly double leftRange, rightRange;
    double? direction;
    public Swivel(double range) {
        leftRange = rightRange = range / 2;
    }
    public Swivel(double left, double right) {
        leftRange = left;
        rightRange = right;
    }
    public void Update(ActiveObject owner, Weapon weapon) {
        if (weapon.targeting.cycleTargets || true) {
            direction = null;
            weapon.targeting.tracker.CycleWhile(t => {
                if(t == null) {
                    return true;
                }
                var dir = Omnidirectional.GetFireAngle(owner, t, weapon);
                var deltaRad = Main.AngleDiffRad(facing, dir);
                if (deltaRad > 0 ? deltaRad > leftRange : -deltaRad > rightRange) {
                    return true;
                }
                direction = dir;
                Heading.AimLine(owner.world, owner.position + weapon.offset, direction.Value);
                return false;
            });
            //return;
        }
        var targeting = weapon.targeting;
        if (targeting.target == null) {
            direction = null;
            return;
        }
        direction = Omnidirectional.GetFireAngle(owner, targeting.target, weapon);
        var deltaRad = Main.AngleDiffRad(facing, direction.Value);
        if (deltaRad > 0 ? deltaRad > leftRange : -deltaRad > rightRange) {
            direction = null;
            targeting.ClearTarget();
            return;
        }
        Heading.AimLine(owner.world, owner.position + weapon.offset, direction.Value);
    }
    public void Update(Station owner, Weapon weapon) {
        facing = owner.rotation + weapon.angle;
        Update((ActiveObject)owner, weapon);
    }
    public void Update(IShip owner, Weapon weapon) {
        facing = owner.rotationRad + weapon.angle;
        Update((ActiveObject)owner, weapon);
    }
    public double? GetFireAngle() => direction;
}

public interface IAmmo {
    bool AllowFire { get; }
    public void Update(IShip source) { }
    public void Update(Station source) { }
    void CheckFire(ref bool firing) => firing &= AllowFire;
    void OnFire();
}
public class ChargeAmmo : IAmmo {
    public int charges;
    public bool AllowFire => charges > 0;
    public ChargeAmmo(int charges) {
        this.charges = charges;
    }
    public void OnFire() => charges--;
}
public class ItemAmmo : IAmmo {
    public ItemType itemType;
    public HashSet<Item> inventory;
    public Item unit;
    public bool AllowFire => unit != null;
    public int count;
    public ItemAmmo(ItemType itemType) =>
        this.itemType = itemType;
    public void Update(IShip source) {
        if (source.world.tick % 120 == 0) {
            Update(source.cargo);
        }
    }
    public void Update(Station source) {
        if(source.world.tick%120 == 0) {
            Update(source.cargo);
        }
    }
    public void Update(HashSet<Item> inventory) {
        this.inventory = inventory;
        if (unit == null || !inventory.Contains(unit)) {
            UpdateUnit();
        }
    }
    public void UpdateUnit() {
        var units = inventory.Where(i => i.type == itemType).ToList();
        unit = units.FirstOrDefault();
        count = units.Count();
    }
    public void OnFire() {
        inventory.Remove(unit);
        UpdateUnit();
    }
}