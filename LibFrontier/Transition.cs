﻿using System;
using System.Collections.Generic;
using System.Linq;
using LibGamer;
namespace RogueFrontier;
public class TitleSlideOpening : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	public Sf sf { get; }
    public int Width => sf.Width;
    public int Height => sf.Height;

	public IScene next;
    public Sf nextSf;
    int x = 0;
    double time = 0;
    double interval;
    bool fast;
    bool updateNext;


    public TitleSlideOpening(IScene next, Sf nextSf, bool updateNext = true) {
        this.sf = new Sf(nextSf.Width, nextSf.Height, Fonts.FONT_8x8);
        x = nextSf.Width;
        this.next = next;
        this.nextSf = nextSf;
        this.updateNext = updateNext;
        interval = 4f / Width;

        //Draw one frame now so that we don't cut out for one frame
        next.Update(new TimeSpan());
        Render(new TimeSpan());
    }
    public void Update(TimeSpan delta) {
        if (updateNext) {
            next.Update(delta);
        }
        if (fast) {
            x -= (int)(Width * delta.TotalSeconds);
        }
        time += delta.TotalSeconds;
        while (time > interval) {
            time -= interval;
            if (x > -16) {
                x--;
            } else {
                Go(next);
                return;
            }
        }
    }
    public void Render(TimeSpan delta) {
        next.Render(delta);
        sf.Clear();
        var blank = new Tile(ABGR.Black, ABGR.Black, ' ');
        for (int y = 0; y < Height; y++) {
            for (int x = 0; x < this.x; x++) {
                sf.SetTile(x, y, new Tile(0, 0, 0));
            }
            for (int x = Math.Max(0, this.x); x < Math.Min(Width, this.x + 16); x++) {

                var glyph = nextSf.GetGlyph(x, y);
                var value = (byte)(255 - 255 / 16 * (x - this.x));

                var fore = nextSf.GetFront(x, y);
                fore = ABGR.Blend(ABGR.Premultiply(fore), ABGR.SetA(ABGR.Black, value));

                var back = nextSf.GetBack(x, y);
                back = ABGR.Blend(ABGR.Premultiply(back), ABGR.SetA(ABGR.Black, value));

                sf.SetTile(x, y, new Tile(fore, back, glyph));
            }
        }
    }
    public void HandleKey(KB kb) {
        if (kb[[KC.Enter, KC.Escape]].Contains(KS.Press)) {
            fast = true;
        }
        return;
    }
}
public class TitleSlideOut : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	public IScene prev, next;
    public Sf sf_prev, sf_next;
    public Sf sf;
    public int Width => sf.Width;
    public int Height => sf.Height;

	int x = 0;
    double time = 0;
    double interval;
    bool fast;
    public TitleSlideOut(IScene prev, Sf sf_prev, IScene next, Sf sf_next) {
        
        this.prev = prev;
        this.next = next;
        this.sf = new Sf(sf_next.Width, sf_next.Height, Fonts.FONT_8x8);
		x = Width;
		interval = 4f / Width;

        //Draw one frame now so that we don't cut out for one frame
        next.Update(new TimeSpan());
        Render(new TimeSpan());
    }
    public void Update(TimeSpan delta) {
        next.Update(delta);

        if (fast) {
            x = -16;
        }

        if (x > -16) {
            x -= (int)(Width * delta.TotalSeconds);
        } else {
            Go(next);
            
            return;
        }
    }
    public void Render(TimeSpan delta) {
        next.Render(delta);
        sf.Clear();
        for (int y = 0; y < Height; y++) {
            for (int x = 0; x < this.x; x++) {
                sf.SetTile(x, y, sf_prev.GetTile(x, y));
            }
            for (int x = Math.Max(0, this.x); x < Math.Min(Width, this.x + 16); x++) {
                var glyph = sf_next.GetGlyph(x, y);
                var value = (byte)(255 - 255 / 16 * (x - this.x));
                var fore = sf_next.GetFront(x, y);
                fore = ABGR.SetA(ABGR.Premultiply(ABGR.Blend(ABGR.Premultiply(fore), sf_prev.GetFront(x, y))), value);
                var back = sf_next.GetBack(x, y);
                back = ABGR.Blend(ABGR.Premultiply(back), ABGR.SetA(ABGR.Premultiply(sf_prev.GetBack(x, y)), value));
                sf.SetTile(x, y, new Tile(fore, back, glyph));
            }
        }
    }
    public void HandleKey(KB keyboard) {
        if (keyboard.Press.Intersect([KC.Enter, KC.Escape]).Any()) {
            fast = true;
        }
    }
}
public class TitleSlideIn : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	public (Sf sf, IScene scene) prev;
    public (Sf sf, IScene scene) next;
    int x = -16;

    Sf sf;
    int Width => sf.Width;
    int Height => sf.Height;


	public TitleSlideIn((Sf sf, IScene scene) prev, (Sf sf, IScene scene) next) {
        sf = new Sf(prev.sf.Width, prev.sf.Height, Fonts.FONT_8x8);
        this.prev = prev;
        this.next = next;
        //Draw one frame now so that we don't cut out for one frame
        Render(new TimeSpan());
    }
    public void ProcessKey(KB keyboard) {
        if (keyboard.IsPress(KC.Enter)) {
            //Tones.pressed.Play();
            Next();
        }
    }
    public void Update(TimeSpan delta) {
        prev.scene.Update(delta);
        if (x < Width + 16) {
            x += (int)(Width * delta.TotalSeconds);
        } else {
            Next();
        }
    }
    public void Next() {
        Go(next.scene);
    }
    public void Render(TimeSpan delta) {

        Draw(next.sf);
        Draw(prev.sf);

        var blank = new Tile(ABGR.Black, ABGR.Black, ' ');
        for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Math.Min(Width, this.x); x++) {
                //this.SetCellAppearance(x, y, blank);
                sf.SetTile(x, y, next.sf.GetTile(x, y));
            }
            //Fading opacity edge
            for (int x = Math.Max(0, this.x); x < Math.Min(Width, this.x + 16); x++) {

                var glyph = prev.sf.GetGlyph(x, y);
                var value = 255 - 255 / 16 * (x - this.x);

                var fore = prev.sf.GetFront(x, y);
                fore = ABGR.Blend(ABGR.Premultiply(fore), ABGR.SetA(ABGR.Premultiply(next.sf.GetFront(x, y)), (byte)value));

                var back = prev.sf.GetBack(x, y);
                back = ABGR.Blend(ABGR.Premultiply(back), ABGR.SetA(ABGR.Premultiply(next.sf.GetBack(x, y)), (byte)value));

                sf.SetTile(x, y, new Tile(fore, back, glyph));
            }
        }
    }
}
public class FadeIn : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	IScene next;
    Sf sf_next;
    Sf sf;
    float alpha;

	public FadeIn(IScene next, Sf sf_next) {
        this.next = next;
        this.sf_next = sf_next;
        this.sf = new Sf(sf_next.Width, sf_next.Height, Fonts.FONT_8x8);
        Render(new TimeSpan());
    }
    public void Update(TimeSpan delta) {
        if (alpha < 1) {
            alpha += (float)(delta.TotalSeconds / 4);
        } else {
            Go(next);
        }
    }
    public void Render(TimeSpan delta) {
        sf.Clear();
        var g = new Tile(ABGR.Black, ABGR.RGBA(0, 0, 0, (byte)(255 * (1 - alpha))), 0);
        for (int y = 0; y < sf.Height; y++) {
            for (int x = 0; x < sf.Width; x++) {
                sf.SetTile(x, y, g);
            }
        }
        Draw(sf_next);
        Draw(sf);
    }
}

public class FadeOut : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	Sf prev;
    Action next;
    float alpha;
    int time;

    Sf sf;
    int Width => sf.Width;
    int Height => sf.Height;
    public FadeOut(Sf prev, Action next, int time = 4) {
        this.prev = prev;
        this.sf = new Sf(prev.Width, prev.Height, Fonts.FONT_8x8);
        this.next = next;
        this.time = time;
        Render(new TimeSpan());
    }
    public void Update(TimeSpan delta) {
        if (alpha < 1) {
            alpha += (float)(delta.TotalSeconds / time);
        } else {
            next();
        }
    }
    public void Render(TimeSpan delta) {
        sf.Clear();
        var g = new Tile(ABGR.Black, ABGR.RGBA(0, 0, 0, (byte)alpha), ' ');
        for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
                sf.SetTile(x, y, g);
            }
        }
        Draw(prev);
        Draw(sf);
    }
}

public class Pause : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	Action next;
    public Sf prev;
    double time;
	public Pause(Sf prev, Action next, double time = 5) {
        this.prev = prev;
        this.time = time;
        this.next = next;
        Render(new TimeSpan());
    }
    public void ProcessKeyboard(KB keyboard) {
        if (keyboard.IsPress(KC.Enter)) {
            //Tones.pressed.Play();
            time = 0;
        }
    }
    public void Update(TimeSpan delta) {
        if (time > 0) {
            time -= delta.TotalSeconds;
        } else {
            next();
        }
    }
    public void Render(TimeSpan delta) {
    }
}
public class PauseTransition : IScene {
	public Action<IScene> Go { get; set; }
	public Action<Sf> Draw { get; set; }
	public Action<SoundCtx> PlaySound { get; set; }
	Sf prev;
    Action next;
    double time;
    public PauseTransition(int Width, int Height, double time, Sf prev, Action next) {
        this.time = time;
        this.prev = prev;
        this.next = next;
        Render(new TimeSpan());
    }
    public void Update(TimeSpan delta) {
        if (time > 0) {
            time -= delta.TotalSeconds;
        } else {
            next();
        }
    }
    public void Render(TimeSpan delta) {
        Draw(prev);
    }
}