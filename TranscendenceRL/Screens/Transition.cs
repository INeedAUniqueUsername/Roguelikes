﻿
using SadConsole;
using System;
using System.Collections.Generic;
using System.Text;
using Console = SadConsole.Console;
using SadRogue.Primitives;
using Common;
using SadConsole.Input;

namespace TranscendenceRL {
    public class TitleSlideOut : Console {
        public Console next;
        int x = 0;
        double time = 0;
        double interval;
        bool fast;
        public TitleSlideOut(Console next) : base(next.Width, next.Height) {
            x = next.Width;
            this.next = next;
            interval = 4f / Width;

            //Draw one frame now so that we don't cut out for one frame
            next.Update(new TimeSpan());
            Draw(new TimeSpan());
        }
        public override void Update(TimeSpan delta) {
            next.Update(delta);
            base.Update(delta);
            if (fast) {
                x -= (int)(4 * (x + 16) * delta.TotalSeconds);
            }
            time += delta.TotalSeconds;
            while(time > interval) {
                time -= interval;
                if (x > -16) {
                    x--;
                } else {
                    SadConsole.Game.Instance.Screen = next;
                    next.IsFocused = true;
                    return;
                }
            }
        }
        public override void Draw(TimeSpan delta) {
            next.Draw(delta);
            base.Draw(delta);
            this.Clear();
            var blank = new ColoredGlyph(Color.Black, Color.Black);
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < this.x; x++) {
                    this.SetCellAppearance(x, y, blank);
                }
                for(int x = Math.Max(0, this.x); x < Math.Min(Width, this.x + 16); x++) {
                    
                    var glyph = next.GetGlyph(x, y);
                    var value = 255 - 255 / 16 * (x - this.x);

                    var fore = next.GetForeground(x, y);
                    fore = fore.Premultiply().Blend(Color.Black.WithValues(alpha: value));

                    var back = next.GetBackground(x, y);
                    back = back.Premultiply().Blend(Color.Black.WithValues(alpha: value));

                    this.SetCellAppearance(x, y, new ColoredGlyph(fore, back, glyph));
                }
            }
        }
        public override bool ProcessKeyboard(Keyboard keyboard) {
            if(keyboard.IsKeyPressed(Keys.Enter)) {
                fast = true;
            }
            return base.ProcessKeyboard(keyboard);
        }
    }
    public class TitleSlideIn : Console {
        public Console prev;
        public Console next;
        int x = -16;
        public TitleSlideIn(Console prev, Console next) : base(prev.Width, prev.Height) {
            this.prev = prev;
            this.next = next;
            //Draw one frame now so that we don't cut out for one frame
            Draw(new TimeSpan());
        }
        public override void Update(TimeSpan delta) {
            prev.Update(delta);
            base.Update(delta);
            if (x < Width) {
                x += (int)(Width * delta.TotalSeconds);
            } else {
                SadConsole.Game.Instance.Screen = next;
                next.IsFocused = true;
            }
        }
        public override void Draw(TimeSpan delta) {
            prev.Draw(delta);
            base.Draw(delta);
            var blank = new ColoredGlyph(Color.Black, Color.Black);
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < this.x; x++) {
                    this.SetCellAppearance(x, y, blank);
                }
                //Fading opacity edge
                for (int x = Math.Max(0, this.x); x < Math.Min(Width, this.x + 16); x++) {

                    var glyph = prev.GetGlyph(x, y);
                    var value = 255 - 255 / 16 * (x - this.x);

                    var fore = prev.GetForeground(x, y);
                    fore = fore.Premultiply().Blend(Color.Black.WithValues(alpha: value));

                    var back = prev.GetBackground(x, y);
                    back = back.Premultiply().Blend(Color.Black.WithValues(alpha: value));

                    this.SetCellAppearance(x, y, new ColoredGlyph(fore, back, glyph));
                }
            }
        }
    }
    public class FadeIn : Console {
        Console next;
        double alpha;
        public FadeIn(Console next) : base(next.Width, next.Height) {
            this.next = next;
            DefaultBackground = Color.Black;
            Draw(new TimeSpan());
        }
        public override void Update(TimeSpan delta) {
            next.Update(delta);
            base.Update(delta);
            if (alpha < 1) {
                alpha += delta.TotalSeconds * Math.Max((1 - alpha) * 4, 1);
            } else {
                SadConsole.Game.Instance.Screen = next;
                next.IsFocused = true;
            }
        }
        public override void Draw(TimeSpan delta) {
            next.Draw(delta);
            base.Draw(delta);
            this.Clear();
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    var glyph = next.GetGlyph(x, y);
                    var foreground = next.GetForeground(x, y);
                    var background = next.GetBackground(x, y);
                    foreground = foreground.WithValues(alpha: (int)(foreground.A * alpha));
                    background = background.WithValues(alpha: (int)(background.A * alpha));
                    this.SetCellAppearance(x, y, new ColoredGlyph(foreground, background, glyph));
                }
            }
        }
    }

    public class Pause : Console {
        Console next;
        double time;
        public Pause(Console next) : base(next.Width, next.Height) {
            this.next = next;
            DefaultBackground = Color.Black;
            time = 5;
            Draw(new TimeSpan());
        }
        public override void Update(TimeSpan delta) {
            base.Update(delta);
            if (time > 0) {
                time -= delta.TotalSeconds;
            } else {
                SadConsole.Game.Instance.Screen = next;
                next.IsFocused = true;
            }
        }
        public override void Draw(TimeSpan delta) {
            base.Draw(delta);
            next.Draw(delta);
        }
    }
}