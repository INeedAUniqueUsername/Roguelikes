﻿using SadConsole;
using LibGamer;
using static Common.Main;
using System.Xml.Linq;
using System.Reflection;
using SfmlFrontier;
using System.Collections.Concurrent;
using SFML.Audio;
namespace RogueFrontier;
partial class Program {
    public static int WIDTH = 100, HEIGHT = 60;
	public static string main = ExpectFile($"{Assets.ROOT}/scripts/Main.xml");
    public static string cover = ExpectFile($"{Assets.ROOT}/sprites/game_title.dat");
    public static string splash = ExpectFile($"{Assets.ROOT}/sprites/game_splash_background.dat");

	static void OutputSchema() {
        var d = new Dictionary<Type, XElement>();
        WriteSchema(typeof(ItemType), d);
        var module = new XElement("Schema");
        foreach (var (key, value) in d) {
            module.Add(value);
        }
        File.WriteAllText("RogueFrontierSchema.xml", module.ToString());
    }
    static void Main(string[] args) {
#if false
        XSave x = null;

        var o = GenerateIntroSystem();
        var map = new XMap(typeof(SoundBufferPort));
        var d = new XSave() { map = map };
        d.SavePointer(o);
        var s1 = d.root.ToString();

        var o2 = d.root.Load();
        d = new XSave() { map = map };
        d.SavePointer(o2);
        var s2 = d.root.ToString();
        var diff = s1.Length - s2.Length;

		Console.WriteLine($"{diff}");

        if (true) return;
#endif
        //OutputSchema();
        SadConsole.Settings.WindowTitle = $"Rogue Frontier v{Assembly.GetExecutingAssembly().GetName().Version}";
        /*
        var w = new System();
        w.types.LoadFile(main);
        string s = "";
        foreach(var type in w.types.Get<ItemType>()) {
            s += (@$"{'\n'}{{""{type.codename}"", {type.value}}}");
        }
        */
        StartGame(StartRegular);
    }

	public static void StartGame(Action<GameHost> OnStart) {
        if (!Directory.Exists("save"))
            Directory.CreateDirectory("save");
        //SadConsole.Host.Settings.SFMLSurfaceBlendMode = SFML.Graphics.BlendMode.Add;
        Game.Create(WIDTH, HEIGHT, Fonts.IBMCGA_8X8_FONT, (o, gh) => { });
        SadConsole.Host.Settings.SFMLScreenBlendMode = SFML.Graphics.BlendMode.Alpha;
        SadConsole.Host.Settings.SFMLSurfaceBlendMode = SFML.Graphics.BlendMode.Alpha;

		Game.Instance.Started += (o, gh)=>OnStart(gh);
        Game.Instance.Run();
        Game.Instance.Dispose();
    }
    public static System GenerateIntroSystem() {
        var w = new System();
        w.types.LoadFile(main);
        if(w.types.TryLookup<SystemType>("system_intro", out var s)) {
            s.Generate(w);
        }
        return w;
    }
    public static void StartRegular(GameHost host) {
#if false
            GameHost.Instance.Screen = new BackdropConsole(Width, Height, new Backdrop(), () => new Common.XY(0.5, 0.5));
			return;
#endif
        ConcurrentDictionary<Sf, SadConsole.Console> consoles = new();
        ConcurrentDictionary<byte[], Sound> sounds = new();  
		IScene current = null;
		Go(new TitleScreen(WIDTH, HEIGHT, GenerateIntroSystem()));
		void Go (IScene next) {
			if(current is { } prev) {
				prev.Go -= Go;
				prev.Draw -= Draw;
                prev.PlaySound -= PlaySound;
			}
			if(next == null)
				throw new Exception("Main scene cannot be null");
			current = next;
			
            current.Go += Go;
			current.Draw += Draw;
            current.PlaySound += PlaySound;
		};
		void Draw(Sf sf) {
            var c = consoles.GetOrAdd(sf, sf => {
                var f = sf.font;
                IFont font = null;
				if(!GameHost.Instance.Fonts.TryGetValue(f.name, out font)) {
					var t = GameHost.Instance.GetTexture(new MemoryStream(f.data));
					font = new SadFont(f.GlyphWidth, f.GlyphHeight, 0, f.rows, f.cols, f.solidGlyphIndex, t, f.name);
					GameHost.Instance.Fonts[f.name] = font;
				}
				var c = new SadConsole.Console(sf.Width, sf.Height) {
                    Position = new(sf.pos.xi, sf.pos.yi),
                    Font = font,
                };
                //c.FontSize *= sf.scale;
                return c;
            });
            c.Clear();
            foreach(var ((x,y),t) in sf.Active) {
				c.SetCellAppearance(x, y, t.ToCG());
            }
            c.Render(new TimeSpan());
            return;
        }
        void PlaySound(SoundCtx s) {
            var snd = sounds.GetOrAdd(s.data, _ => {
                var snd = new Sound(new SoundBuffer(s.data)) { Volume = s.volume };
                //s.IsPlaying = () => snd.Status == SoundStatus.Playing;
                return snd;
			});
            if(snd.Status == SoundStatus.Playing) {
                snd.Stop();
            }
            snd.Volume = s.volume;
            snd.Position = new SFML.System.Vector3f(s.pos.x, s.pos.y, 0);
            snd.Play();
        }
        var kb = new KB();
        host.FrameUpdate += (o, gh) => {

			kb.Update([.. gh.Keyboard.KeysDown.Select(k => (KC)k.Key)]);
			var m = gh.Mouse;

            if(current is { } c) {
                c.Update(gh.UpdateFrameDelta);
                c.HandleKey(kb);
                c.HandleMouse(new HandState(m.ScreenPosition, m.ScrollWheelValue, m.LeftButtonDown, m.MiddleButtonDown, m.RightButtonDown, m.IsOnScreen));
            }
        };
        host.FrameRender += (o, gh) => {
            current.Render(gh.DrawFrameDelta);
        };

#if false
        //var files = Directory.GetFiles($"{AppDomain.CurrentDomain.BaseDirectory}save", "*.trl");
        //SaveGame.Deserialize(File.ReadAllText(files.First()));

        var splashMusic = new Sound(new SoundBuffer("Assets/music/Splash.wav")) {
            Volume = 33
        };
        var poster = new TileImage(ImageLoader.DeserializeObject<Dictionary<(int, int), TileTuple>>(File.ReadAllText(cover)));

        var title = new TitleScreen(WIDTH, HEIGHT, GenerateIntroSystem());
        var titleSlide = new TitleSlideOpening(title) { IsFocused = true };

        var splashBack = new TileImage(ImageLoader.DeserializeObject<Dictionary<(int, int), TileTuple>>(File.ReadAllText(splash)));
        var splashBackground = new ImageDisplay(WIDTH / 2, HEIGHT / 2, splashBack, new Point()) { FontSize = title.FontSize * 2 };

        int index = 0;
        KeyWatcher container = null;
        container = new KeyWatcher(WIDTH, HEIGHT, (k) => {
            if (k.IsKeyPressed(Keys.Enter)) {
                switch (index) {
                    case 1: {
                            container.Children.Clear();
                            Con c = new(WIDTH, HEIGHT);
                            container.Children.Add(c);
                            ShowOpening(c);
                            break;
                        }
                    case 2: {
                            container.Children.Clear();
                            Con c = new(WIDTH, HEIGHT);
                            container.Children.Add(c);
                            ShowPoster(c);
                            break;
                        }
                    case 3:
                    default:
                        ShowTitle();
                        break;
                }

            }
        }) {
            IsFocused = true, UseKeyboard = true,
        };
        container.Children.Add(splashBackground);

        GameHost.Instance.Screen = container;


#if DEBUG
        ShowTitle();
        //title.QuickStart();
        //title.StartSurvival();
#else
            ShowSplash();
#endif

        void ShowSplash() {


            //var p = Path.GetFullPath(theme);
            //MediaPlayer.Play(Song.FromUri(p, new(p)));

            index = 1;
            SplashScreen c = null;
            c = new SplashScreen(() => ShowCrawl(c));
            container.Children.Add(c);
        }
        void ShowCrawl(Con prev) {
            MinimalCrawlScreen c = null;
            string s = "Presents...";
            c = new MinimalCrawlScreen(s, () => {
                ShowPause(prev);
            }) { Position = new Point(prev.Width / 4 - s.Length / 2 + 1, 13), FontSize = prev.FontSize * 2 };
            prev.Children.Add(c);
        }
        void ShowPause(Con prev) {
            Con c = null;
            c = new PauseTransition(WIDTH, HEIGHT, 1, prev, () => ShowFade(c));

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }
        void ShowFade(Con prev) {
            Con c = null;
            c = new FadeOut(prev, () => ShowOpening(c), 1);

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }

        void ShowOpening(Con prev) {
            splashMusic.Play();
            index = 2;
            prev.Parent.Children.Remove(splashBackground);

            Con c = null;
            c = new MinimalCrawlScreen(
@"                  
A reimagining of...
                    
      Transcendence  
 by George Moromisato
                    
And the vision that was
more than just a dream...
                    ".Replace("\r", null), () => ShowFade2(c)) {
                Position = new Point(4, 4),
                FontSize = title.FontSize * 3
            };

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }
        /*
        void ShowPause2(Console prev) {
            Console c = null;
            c = new PauseTransition(Width, Height, 1, prev, () => ShowFade2(c));

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }
        */
        void ShowFade2(Con prev) {
            Con c = null;
            c = new FadeOut(prev, () => ShowPause2(c), 1);

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }
        void ShowPause2(Con prev) {
            Con c = null;
            c = new PauseTransition(WIDTH, HEIGHT, 1, prev, () => ShowPoster(c));

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }
        void ShowPoster(Con prev) {
            index = 3;
            var display = new ImageDisplay(poster.Size.X, poster.Size.Y, poster,
                new Point(WIDTH / 2 - poster.Size.X / 2 + 4, -5)) {
                FontSize = title.FontSize * 3 / 4
            };

            Con pause = null;
            pause = new PauseTransition(display.Width, display.Height, 2, display, () => ShowPosterFade(pause)) {
                FontSize = display.FontSize
            };

            //Note that FadeIn automatically replaces the child console
            var c = new FadeIn(pause);

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }
        void ShowPosterFade(Con prev) {
            var c = new FadeOut(prev, ShowTitle, 1);

            prev.Parent.Children.Add(c);
            prev.Parent.Children.Remove(prev);
        }

        void ShowTitle() {

            splashMusic.Stop();
            title.titleMusic.Play();
            index = 4;
            titleSlide.IsFocused = true;
            GameHost.Instance.Screen = titleSlide;
        }
        //GameHost.Instance.Screen = new TitleDraw();
#endif
	}
}
