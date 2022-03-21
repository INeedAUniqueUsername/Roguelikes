﻿using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Console = SadConsole.Console;
namespace RogueFrontier;

public class GateTransition : Console {
    Viewport prev, next;
    double amount;
    Rectangle rect;
    public Action Transition;

    class Particle {
        public int lifetime;
        public Point pos;
        public Particle(int lifetime, Point pos) {
            this.lifetime = lifetime;
            this.pos = pos;
        }
    }
    private List<Particle> particles = new();
    public GateTransition(Viewport prev, Viewport next, Action Transition) : base(prev.Width, prev.Height) {
        this.prev = prev;
        this.next = next;
        DefaultBackground = Color.Transparent;
        rect = new(new(Width / 2, Height / 2), 0, 0);
        this.Transition = Transition;
    }
    public override bool ProcessKeyboard(Keyboard keyboard) {
        if (keyboard.IsKeyPressed(Keys.Enter)) {
            Transition();
        }
        return base.ProcessKeyboard(keyboard);
    }
    public override void Update(TimeSpan delta) {
        prev.Update(delta);
        //next.Update(delta);
        base.Update(delta);
        amount += delta.TotalSeconds * 1.2;

        if (amount < 1) {
            rect = new Rectangle(new(Width / 2, Height / 2), (int)(amount * Width / 2), (int)(amount * Height / 2));
            particles.AddRange(rect.PerimeterPositions().Select(p => new Particle(15, p)));
            particles.ForEach(p => p.lifetime--);
            particles.RemoveAll(p => p.lifetime < 1);
        } else {
            Transition();
        }
    }
    public override void Render(TimeSpan delta) {
        this.Clear();

        Console particleLayer = new Console(Width, Height);
        particles.ForEach(p => {
            var pos = p.pos;
            particleLayer.SetBackground(pos.X, pos.Y, new Color(204, 160, 255, p.lifetime * 255 / 15));
        });


        Console back = new Console(Width, Height);

        BackdropConsole prevBack = new(prev);
        BackdropConsole nextBack = new(next);

        foreach (var y in Enumerable.Range(0, Height)) {
            foreach (var x in Enumerable.Range(0, Width)) {
                Point p = new(x, y);
                (var v, var b) = rect.Contains(p) ? (next, nextBack) : (prev, prevBack);
                back.SetCellAppearance(x, y, b.GetTile(x, y));
                ColoredGlyph g = v.GetTile(x, y);
                //var g = (rect.Contains(p) ? next : prev).GetCellAppearance(x, y);
                this.SetCellAppearance(x, Height - y, g);

            }
        }
        back.Render(delta);
        base.Render(delta);
        particleLayer.Render(delta);
    }
}