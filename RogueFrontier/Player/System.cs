﻿using Common;
using Newtonsoft.Json;
using SadConsole;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueFrontier;

class EffectLocator : ILocator<Effect, (int, int)> {
    public (int, int) Locate(Effect e) => (e.position.xi, e.position.yi);
}
class EntityLocator : ILocator<Entity, (int, int)> {
    public (int, int) Locate(Entity e) => (e.position.xi, e.position.yi);
}
public class System {
    [JsonIgnore]
    public static readonly System empty = new System(new Universe());

    public string id, name;

    public HashSet<Event> events = new HashSet<Event>();
    public List<Event> eventsAdded = new List<Event>();
    public List<Event> eventsRemoved = new List<Event>();

    public LocatorDict<Entity, (int, int)> entities = new LocatorDict<Entity, (int, int)>(new EntityLocator());
    public List<Entity> entitiesAdded = new List<Entity>();
    public List<Entity> entitiesRemoved = new List<Entity>();
    public LocatorDict<Effect, (int, int)> effects = new LocatorDict<Effect, (int, int)>(new EffectLocator());
    public List<Effect> effectsAdded = new List<Effect>();
    public List<Effect> effectsRemoved = new List<Effect>();

    public List<Star> stars = new List<Star>();

    public Universe universe;
    [JsonIgnore]
    public TypeCollection types => universe.types;
    [JsonIgnore]
    public Rand karma => universe.karma;
    public Backdrop backdrop=new();

    public int tick;
    public ulong nextId;

    private bool updating;

    public System() {
        this.universe = new();
    }
    public System(Universe universe) {
        this.universe = universe;
    }
    public void AddEvent(Event e) {
        eventsAdded.Add(e);
        return;
        if (updating) eventsAdded.Add(e);
        else events.Add(e);
    }
    public void AddEffect(Effect e) {
        effectsAdded.Add(e);
        return;
        if (updating) effectsAdded.Add(e);
        else effects.all.Add(e);
    }
    public void AddEntity(Entity e) {
        entitiesAdded.Add(e);
        return;
        if (updating) entitiesAdded.Add(e);
        else entities.all.Add(e);
    }
    public void RemoveEvent(Event e) {
        eventsRemoved.Add(e);
        return;
        if (updating) eventsRemoved.Add(e);
        else events.Add(e);
    }
    public void RemoveEffect(Effect e) {
        effectsRemoved.Add(e);
        return;
        if (updating) effectsRemoved.Add(e);
        else effects.all.Add(e);
    }
    public void RemoveEntity(Entity e) {
        entitiesRemoved.Add(e);
        return;
        if (updating) entitiesRemoved.Add(e);
        else entities.all.Remove(e);
    }
    public void RemoveAll() {
        events.Clear();
        entities.Clear();
        effects.Clear();
        eventsAdded.Clear();
        entitiesAdded.Clear();
        effectsAdded.Clear();
        eventsRemoved.Clear();
        entitiesRemoved.Clear();
        effectsRemoved.Clear();
    }
    public void UpdateAdded() {
        events.UnionWith(eventsAdded);
        entities.all.UnionWith(entitiesAdded);
        effects.all.UnionWith(effectsAdded);
        eventsAdded.Clear();
        entitiesAdded.Clear();
        effectsAdded.Clear();
    }
    public void UpdateRemoved() {
        events.ExceptWith(eventsRemoved);
        entities.all.ExceptWith(entitiesRemoved);
        effects.all.ExceptWith(effectsRemoved);
        eventsRemoved.Clear();
        entitiesRemoved.Clear();
        effectsRemoved.Clear();

        entities.all.RemoveWhere(e => !e.active);
        effects.all.RemoveWhere(e => !e.active);
        events.RemoveWhere(e => !e.active);
    }
    public void UpdatePresent() {
        UpdateAdded();
        UpdateRemoved();
    }
    public void UpdateSpace() {
        //Place everything in the grid
        entities.UpdateSpace();
        effects.UpdateSpace();
    }
    public void UpdateActive() {
        UpdateSpace();

        //updating = true;
        //Update everything
        foreach (var e in entities.all) {
            e.Update();
        }
        foreach (var e in effects.all) {
            e.Update();
        }
        foreach (var e in events) {
            e.Update();
        }
        //updating = false;

        tick++;
    }

    public void PlaceTiles(Dictionary<(int, int), ColoredGlyph> tiles) {
        foreach (var e in entities.all) {
            if (e.tile == null) continue;
            var p = e.position.roundDown;
            if (!tiles.ContainsKey(p) || e is ISegment) {
                tiles[p] = e.tile;
            }
        }
        foreach (var e in effects.all) {
            if (e.tile == null) continue;
            var p = e.position.roundDown;
            if (!tiles.ContainsKey(p)) {
                tiles[p] = e.tile;
            }
        }
    }
    public void PlaceTilesVisible(Dictionary<(int, int), ColoredGlyph> tiles, Func<Entity, double> getVisibleDistanceLeft) {
        
        Dictionary<(int, int), List<ColoredGlyph>> all = new();
        List<ColoredGlyph> Initialize((int, int) key) => 
            all.TryGetValue(key, out var l) ? l : all[key] = new(10);
        foreach (var e in entities.all) {
            if (e.tile == null) continue;
            var dist = getVisibleDistanceLeft(e);
            if (dist<0) {
                continue;
            }
            var t = e.tile;
            const double threshold = 8;
            if (dist < threshold) {
                t = t.Clone();
                t.Foreground = t.Foreground.SetAlpha((byte)(255 * dist / threshold));
            }
            var p = e.position.roundDown;
            Initialize(p).Add(t);
        }
        foreach (var e in effects.all) {
            if (e.tile == null) continue;
            var p = e.position.roundDown;
            Initialize(p).Add(e.tile);
        }

        int time = tick / 30;
        foreach((var p, var set) in all) {
            tiles[p] = set[time % set.Count];
        }
    }

    public void PlaceTilesOver(Dictionary<(int, int), ColoredGlyph> tiles, Func<Entity, double> getVisibleDistanceLeft) {
        //Add probabilistic overwrites
        //To do: Handle stealth
        foreach (var e in entities.all) {
            if (e.tile == null) continue;


            var dist = getVisibleDistanceLeft(e);
            if (dist < 0) {
                continue;
            }

            var t = e.tile;
            const double threshold = 16;
            if (dist < threshold) {
                t = t.Clone();
                t.Background = t.Background.SetAlpha((byte)(255 * dist / threshold));
            }

            tiles[e.position.roundDown] = t;
        }
        foreach (var e in effects.all) {
            if (e.tile == null) continue;
            tiles[e.position.roundDown] = e.tile;
        }
    }

    public Stargate FindGateTo(System to) => universe.FindGateTo(this, to);
}
