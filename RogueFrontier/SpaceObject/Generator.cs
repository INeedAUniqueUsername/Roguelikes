﻿using Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static RogueFrontier.Weapon;

namespace RogueFrontier;

public interface ShipGenerator {
    List<AIShip> Generate(TypeCollection tc, SpaceObject owner);
    public void GenerateAndPlace(TypeCollection tc, SpaceObject owner) {
        var w = owner.world;
        Generate(tc, owner)?.ForEach(s => {
            w.AddEntity(s);
            w.AddEffect(new Heading(s));
        });
    }
}
public class ShipList : ShipGenerator {
    public List<ShipGenerator> generators;
    public ShipList() { generators = new List<ShipGenerator>(); }
    public ShipList(XElement e) {
        generators = new List<ShipGenerator>();
        foreach (var element in e.Elements()) {
            switch (element.Name.LocalName) {
                case "Ship":
                    generators.Add(new ShipEntry(element));
                    break;
                default:
                    throw new Exception($"Unknown <Ships> subelement {element.Name}");
            }
        }
    }
    public List<AIShip> Generate(TypeCollection tc, SpaceObject owner) {
        var result = new List<AIShip>();
        generators.ForEach(g => result.AddRange(g.Generate(tc, owner)));
        return result;
    }
}
public enum ShipOrder {
    attack, guard, patrol, patrolCircuit, 

}
public class ShipEntry : ShipGenerator {
    [Opt] public int count = 1;
    [Req] public string codename;
    [Opt] public string sovereign;
    public IOrderDesc orderDesc;
    public ShipEntry() { }
    public ShipEntry(XElement e) {
        e.Initialize(this);
        orderDesc = e.TryAttEnum("order", ShipOrder.guard) switch {
            ShipOrder.attack => new AttackDesc(),
            ShipOrder.guard => new GuardDesc(),
            ShipOrder.patrol => new PatrolOrbitDesc(e),
            ShipOrder.patrolCircuit => new PatrolCircuitDesc(e)
        };
    }
    public List<AIShip> Generate(TypeCollection tc, SpaceObject owner) {
        var shipClass = tc.Lookup<ShipClass>(codename);
        Sovereign s = sovereign?.Any() == true ? tc.Lookup<Sovereign>(sovereign) : owner.sovereign;
        Func<int, XY> GetPos = orderDesc switch {
            PatrolOrbitDesc pod => i => owner.position + XY.Polar(
                                        Math.PI * 2 * i / count,
                                        pod.patrolRadius),
            _ => i => owner.position
        };
        return new List<AIShip>(
            Enumerable.Range(0, count)
            .Select(i => new AIShip(new BaseShip(
                    owner.world,
                    shipClass,
                    s,
                    GetPos(i)
                ),
                orderDesc.Value(owner)
                ))
            );
    }
    //In case we want to make sure immediately that the type is valid
    public void ValidateEager(TypeCollection tc) {
        if (!tc.Lookup<ShipClass>(codename, out var shipClass)) {
            throw new Exception($"Invalid ShipClass type {codename}");
        }
        if (sovereign.Any() && !tc.Lookup<Sovereign>(sovereign, out var sov)) {
            throw new Exception($"Invalid Sovereign type {sovereign}");
        }
    }

    public interface IOrderDesc : IContainer<IShipOrder.Create> {}
    public record AttackDesc : IOrderDesc {
        [JsonIgnore]
        public IShipOrder.Create Value => o => new AttackOrder(o);
    }
    public record GuardDesc : IOrderDesc {
        [JsonIgnore]
        public IShipOrder.Create Value => o => new GuardOrder(o);
    }
    public record PatrolOrbitDesc() : IOrderDesc {
        [Req] public int patrolRadius;
        public PatrolOrbitDesc(XElement e) : this() {
            e.Initialize(this);
        }
        [JsonIgnore]
        public IShipOrder.Create Value => o => new PatrolOrbitOrder(o, patrolRadius);
    }
    //Patrol an entire cluster of stations (moving out to 50 ls + radius of nearest station)
    public record PatrolCircuitDesc() : IOrderDesc {
        public int patrolRadius;
        public PatrolCircuitDesc(XElement e) : this() {
            e.Initialize(this);
        }
        [JsonIgnore]
        public IShipOrder.Create Value => o => new PatrolCircuitOrder(o, patrolRadius);
    }
}

public record ModRoll() {
    public double modifierChance;
    public Modifier modifier;
    public ModRoll(XElement e) : this() {
        modifierChance = e.TryAttDouble(nameof(modifierChance), 1);
        modifier = new Modifier(e);
        if (modifier.empty) {
            modifier = null;
        }
    }
    public Modifier Generate() {
        if (modifier == null) {
            return null;
        }
        return new Rand().NextDouble() <= modifierChance ? modifier : null;
    }
}
public interface Generator<T> {
    List<T> Generate(TypeCollection t);
}
public record ItemList() : Generator<Item> {
    public List<Generator<Item>> generators;
    public static List<Item> From(TypeCollection tc, string str) => new ItemList(XElement.Parse(str)).Generate(tc);
    public ItemList(XElement e) : this() {
        generators = new List<Generator<Item>>();
        foreach (var element in e.Elements()) {
            switch (element.Name.LocalName) {
                case "Item":
                    generators.Add(new ItemEntry(element));
                    break;
                default:
                    throw new Exception($"Unknown <Items> subelement {element.Name}");
            }
        }
    }
    public List<Item> Generate(TypeCollection tc) =>
        new(generators.SelectMany(g => g.Generate(tc)));
}
public record ItemEntry() : Generator<Item> {
    [Req] public string codename;
    [Opt] public int count = 1;
    public ModRoll mod;
    public ItemEntry(XElement e) : this() {
        e.Initialize(this);
        mod = new(e);
    }
    public List<Item> Generate(TypeCollection tc) {
        var type = tc.Lookup<ItemType>(codename);
        return new List<Item>(Enumerable.Range(0, count).Select(_ => new Item(type, mod.Generate())));
    }
    //In case we want to make sure immediately that the type is valid
    public void ValidateEager(TypeCollection tc) =>
        tc.Lookup<ItemType>(codename);
}
public interface ArmorGenerator {
    List<Armor> Generate(TypeCollection tc);
}
public record ArmorList() : ArmorGenerator {
    public List<ArmorGenerator> generators;
    public ArmorList(XElement e) : this() {
        generators = new List<ArmorGenerator>();
        foreach (var element in e.Elements()) {
            switch (element.Name.LocalName) {
                case "Armor":
                    generators.Add(new ArmorEntry(element));
                    break;
                default:
                    throw new Exception($"Unknown <Armor> subelement {element.Name}");
            }
        }
    }
    public List<Armor> Generate(TypeCollection tc) =>
        new(generators.SelectMany(g => g.Generate(tc)));
}
public record ArmorEntry() : ArmorGenerator {
    [Req] public string codename;
    public ModRoll mod;
    public ArmorEntry(XElement e) : this() {
        e.Initialize(this);
        mod = new(e);
    }
    List<Armor> ArmorGenerator.Generate(TypeCollection tc) =>
        new() { Generate(tc) };
    public Armor Generate(TypeCollection tc) =>
        SDevice.Generate<Armor>(tc, codename, mod);
    public void ValidateEager(TypeCollection tc) =>
        Generate(tc);
    /*
    public interface Generator<T> where T: Device {
        List<T> Generate(TypeCollection tc);
    }
    public class GeneratorList<T> : Generator<T> where T: Device {
        public List<Generator<T>> generators;
        public GeneratorList(XElement e) {

        }
        public List<T> Generate(TypeCollection tc) {
            var result = new List<T>();
            generators.ForEach(g => result.AddRange(g.Generate(tc)));
            return result;
        }
    }
    */

}
public static class SDevice {
    private static T Install<T>(TypeCollection tc, string codename, ModRoll mod) where T : class, Device =>
        new Item(tc.Lookup<ItemType>(codename), mod.Generate()).Install<T>();
    public static T Generate<T>(TypeCollection tc, string codename, ModRoll mod) where T : class, Device =>
        Install<T>(tc, codename, mod) ??
            throw new Exception($"Expected <ItemType> type with <{typeof(T).Name}> desc: {codename}");
}
public record DeviceList() : Generator<Device> {
    public List<Generator<Device>> generators;
    public DeviceList(XElement e) : this() {
        generators = new List<Generator<Device>>();
        foreach (var element in e.Elements()) {
            switch (element.Name.LocalName) {
                case "Weapon":
                    generators.Add(new WeaponEntry(element));
                    break;
                case "Shield":
                    generators.Add(new ShieldEntry(element));
                    break;
                case "Reactor":
                    generators.Add(new ReactorEntry(element));
                    break;
                case "Solar":
                    generators.Add(new SolarEntry(element));
                    break;
                case "Service":
                    generators.Add(new ServiceEntry(element));
                    break;
                default:
                    throw new Exception($"Unknown <Devices> subelement <{element.Name}>");
            }
        }
    }
    public List<Device> Generate(TypeCollection tc) =>
        new(generators.SelectMany(g => g.Generate(tc)));
}

public record ReactorEntry() : Generator<Device> {
    [Req] public string codename;
    public ModRoll mod;
    public ReactorEntry(XElement e) : this() {
        e.Initialize(this);
        mod = new(e);
    }
    List<Device> Generator<Device>.Generate(TypeCollection tc) =>
        new() { Generate(tc) };
    Reactor Generate(TypeCollection tc) =>
        SDevice.Generate<Reactor>(tc, codename, mod);
    public void ValidateEager(TypeCollection tc) => Generate(tc);
}

public record SolarEntry() : Generator<Device> {
    [Req] public string codename;
    public ModRoll mod;
    public SolarEntry(XElement e) : this() {
        e.Initialize(this);
        mod = new(e);
    }
    List<Device> Generator<Device>.Generate(TypeCollection tc) =>
        new() { Generate(tc) };
    Solar Generate(TypeCollection tc) =>
        SDevice.Generate<Solar>(tc, codename, mod);
    public void ValidateEager(TypeCollection tc) => Generate(tc);
}

public record ServiceEntry() : Generator<Device> {
    [Req] public string codename;

    public ModRoll mod;
    public ServiceEntry(XElement e) : this() {
        e.Initialize(this);
        mod = new(e);
    }
    List<Device> Generator<Device>.Generate(TypeCollection tc) =>
        new() { Generate(tc) };
    Service Generate(TypeCollection tc) => SDevice.Generate<Service>(tc, codename, mod);
    public void ValidateEager(TypeCollection tc) => Generate(tc);
}

public record ShieldEntry() : Generator<Device> {
    public string codename;

    public ModRoll mod;
    public ShieldEntry(XElement e) : this() {
        codename = e.ExpectAtt("codename");
        mod = new(e);
    }
    List<Device> Generator<Device>.Generate(TypeCollection tc) =>
        new() { Generate(tc) };
    Shield Generate(TypeCollection tc) =>
        SDevice.Generate<Shield>(tc, codename, mod);
    public void ValidateEager(TypeCollection tc) => Generate(tc);
}
public record WeaponList() : Generator<Weapon> {
    public List<Generator<Weapon>> generators;
    public WeaponList(XElement e) : this() {
        generators = new List<Generator<Weapon>>();
        foreach (var element in e.Elements()) {
            switch (element.Name.LocalName) {
                case "Weapon":
                    generators.Add(new WeaponEntry(element));
                    break;
                default:
                    throw new Exception($"Unknown <Weapons> subelement {element.Name}");
            }
        }
    }
    public List<Weapon> Generate(TypeCollection tc) =>
        new(generators.SelectMany(g => g.Generate(tc)));
}
public record WeaponEntry() : Generator<Device>, Generator<Weapon> {
    public string codename;
    public bool omnidirectional;
    public ModRoll mod;
    public WeaponEntry(XElement e) : this() {
        codename = e.ExpectAtt("codename");
        omnidirectional = e.TryAttBool("omnidirectional", false);
        mod = new(e);
    }

    List<Weapon> Generator<Weapon>.Generate(TypeCollection tc) =>
        new() { Generate(tc) };
    List<Device> Generator<Device>.Generate(TypeCollection tc) =>
        new() { Generate(tc) };

    Weapon Generate(TypeCollection tc) {
        var w = SDevice.Generate<Weapon>(tc, codename, mod);
        if (omnidirectional) {
            w.aiming = new Omnidirectional();
        }
        return w;
    }
    public void ValidateEager(TypeCollection tc) => Generate(tc);
}