﻿using Newtonsoft.Json;
using SadConsole;
using SadConsole.Input;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SadConsole.Input.Keys;

namespace ASECII {
    class EditorMain : SadConsole.Console {
        SpriteModel model;
        public EditorMain(int width, int height, SpriteModel model) :base(width, height) {
            UseKeyboard = true;
            UseMouse = true;

            this.model = model;
            var tileModel = model.tiles;
            var paletteModel = new PaletteModel();
            var pickerModel = new PickerModel(16, 16, model);

            pickerModel.UpdateColors();
            pickerModel.UpdateBrushPoints(paletteModel);
            pickerModel.UpdatePalettePoints(paletteModel);

            CellButton tileButton = null;
            tileButton = new CellButton(() => {
                var c = model.brush.cell;
                return tileModel.brushIndex == null;
            }, () => {
                tileModel.AddTile(model.brush.cell);
                tileModel.UpdateIndexes(model);
                tileButton.UpdateActive();
            }) {
                Position = new Point(15, 3),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            CellButton foregroundButton = null, backgroundButton = null;
            foregroundButton = new CellButton(() => {
                return paletteModel.foregroundIndex == null;
            }, () => {
                paletteModel.AddColor(model.brush.foreground);
                paletteModel.UpdateIndexes(model);

                PaletteChanged();
            }) {
                Position = new Point(15, 27),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            backgroundButton = new CellButton(() => {
                return paletteModel.backgroundIndex == null;
            }, () => {
                paletteModel.AddColor(model.brush.background);
                paletteModel.UpdateIndexes(model);
                PaletteChanged();
            }) {
                Position = new Point(14, 27),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            void PaletteChanged() {
                foregroundButton.UpdateActive();
                backgroundButton.UpdateActive();

                pickerModel.UpdateColors();
                pickerModel.UpdateBrushPoints(paletteModel);
                pickerModel.UpdatePalettePoints(paletteModel);
            }

            var spriteMenu = new SpriteMenu(32, 32, model) {
                Position = new Point(16, 0),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            var tileMenu = new TileMenu(16, 8, model, tileModel, () => {
                tileModel.UpdateIndexes(model);
                tileButton.UpdateActive();
                
                paletteModel.UpdateIndexes(model);
                foregroundButton.UpdateActive();
                backgroundButton.UpdateActive();

                pickerModel.UpdateColors();
                pickerModel.UpdateBrushPoints(paletteModel);
                pickerModel.UpdatePalettePoints(paletteModel);
            }) {
                Position = new Point(0, 0),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            var glyphMenu = new GlyphMenu(16, 16, model, () => {
                tileModel.UpdateIndexes(model);
                tileButton.UpdateActive();
            }) {
                Position = new Point(0, 8),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            var paletteMenu = new PaletteMenu(16, 4, model, paletteModel, () => {
                tileModel.UpdateIndexes(model);
                tileButton.UpdateActive();

                foregroundButton.UpdateActive();
                backgroundButton.UpdateActive();

                pickerModel.UpdateBrushPoints(paletteModel);

                pickerModel.UpdateColors();
                pickerModel.UpdateBrushPoints(paletteModel);
                pickerModel.UpdatePalettePoints(paletteModel);
            }) {
                Position = new Point(0, 24),
                FocusOnMouseClick = true,
                UseMouse = true
            };

            var pickerMenu = new PickerMenu(16, 16, model, pickerModel, () => {
                tileModel.UpdateIndexes(model);
                tileButton.UpdateActive();

                paletteModel.UpdateIndexes(model);
                foregroundButton.UpdateActive();
                backgroundButton.UpdateActive();
            }) {
                Position = new Point(0, 28),
                FocusOnMouseClick = true,
                UseMouse = true
            };
            var colorBar = new ColorBar(16, 1, paletteModel, pickerModel) {
                Position = new Point(0, 44),
                FocusOnMouseClick = true,
                UseMouse = true
            };

            tileModel.UpdateIndexes(model);
            tileButton.UpdateActive();

            paletteModel.UpdateIndexes(model);
            foregroundButton.UpdateActive();
            backgroundButton.UpdateActive();

            this.Children.Add(tileMenu);
            this.Children.Add(tileButton);
            this.Children.Add(spriteMenu);
            this.Children.Add(glyphMenu);
            this.Children.Add(paletteMenu);
            this.Children.Add(foregroundButton);
            this.Children.Add(backgroundButton);
            this.Children.Add(pickerMenu);
            this.Children.Add(colorBar);
        }
        public override bool ProcessKeyboard(Keyboard info) {
            return base.ProcessKeyboard(info);
        }
        public override void Draw(TimeSpan timeElapsed) {
            this.Clear();
            base.Draw(timeElapsed);
        }
    }

    class SpriteMenu : SadConsole.Console {
        SpriteModel model;
        public SpriteMenu(int width, int height, SpriteModel model) : base(width, height) {
            UseMouse = true;
            UseKeyboard = true;
            this.model = model;
        }
        public override void Update(TimeSpan timeElapsed) {
            base.Update(timeElapsed);
        }
        public override void Draw(TimeSpan timeElapsed) {
            this.Clear();
            model.ticks++;
            model.ticksSelect++;

            int hx = Width / 2;
            int hy = Height / 2;

            var camera = model.camera - model.pan.offsetPan;

            var black = Color.Black;
            var dark = new Color(25, 25, 25);
            model.sprite.UpdatePreview();
            var center = new Point(hx, hy);
            for (int x = -hx; x < hx+1; x++) {
                for(int y = -hy; y < hy+1; y++) {
                    var pos = camera - new Point(x, y) + center;

                    int ax = hx - x;
                    int ay = hy - y;
                    if(model.sprite.InBounds(pos)) {
                        var tile = model.sprite.preview[pos.X, pos.Y];
                        this.Print(ax, ay, tile.cg);
                    } else {
                        var c = ((ax + ay) % 2 == 0) ? black : dark;
                        /*
                        int r = (int)(Math.Sin(2 * pos.X + pos.Y) * 25);
                        int g = (int)(Math.Sin(1 * pos.X + pos.Y) * 25);
                        int b = (int)(Math.Sin(1 * pos.X + pos.Y) * 25);
                        var c = new Color(r, g, b);
                        */
                        this.Print(ax, ay, " ", Color.Transparent, c);
                    }
                }
            }
            if(model.pan.allowPan) {

            } else {
                switch (model.mode) {
                    case Mode.Edit:
                        if (model.ticksSelect % 30 < 15) {
                            if (model.select.GetAdjustedRect(out Rectangle r)) {
                                DrawRect(r);
                            }
                        }
                        if ((model.ticks % 30) < 15) {
                            if (IsMouseOver) {
                                this.SetCellAppearance(model.cursorScreen.X, model.cursorScreen.Y, model.brush.cell);
                            }
                        }
                        

                        break;
                    case Mode.Keyboard:
                        var c = model.brush.cell;
                        if(model.ticksSelect%30 < 15) {
                            if (model.select.GetAdjustedRect(out Rectangle r)) {
                                DrawRect(r);
                            }
                        }
                        if (model.ticks % 30 < 15) {
                            var p = (model.keyboard.keyCursor ?? model.cursor) - camera;
                            this.SetCellAppearance(p.X, p.Y, new ColoredGlyph(c.Foreground, c.Background, '_'));
                            //SetCellAppearance(hx - model.keyboard.margin.X + model.camera.X - 1, hy - model.keyboard.keyCursor.Y + model.camera.Y, new Cell(c.Foreground, c.Background, '>'));
                        }
                        break;
                    case Mode.Select:
                        if (model.ticksSelect % 20 < 10) {
                            if (model.select.GetAdjustedRect(out Rectangle r)) {
                                DrawRect(r);
                            } else {
                                var p = model.cursorScreen;
                                DrawBox(p.X, p.Y, new BoxGlyph { n = Line.Single, e = Line.Single, s = Line.Single, w = Line.Single });
                            }
                        }
                        
                        break;
                }
            }
            
            

            base.Draw(timeElapsed);

            void DrawRect(Rectangle r) {
                var (left, top) = (r.X, r.Y);


                if (r.Width > 0 && r.Height > 0) {
                    DrawSidesX(Line.Single);
                    DrawSidesY(Line.Single);
                    DrawCornerNW();
                    DrawCornerNE();
                    DrawCornerSW();
                    DrawCornerSE();
                } else if (r.Width > 0) {
                    DrawSidesX(Line.Double);
                    DrawBox(left, top, new BoxGlyph { n = Line.Single, e = Line.Double, s = Line.Single });
                    DrawBox(left + r.Width, top, new BoxGlyph { n = Line.Single, w = Line.Double, s = Line.Single });
                } else if (r.Height > 0) {
                    DrawSidesY(Line.Double);
                    DrawBox(left, top, new BoxGlyph { s = Line.Double, e = Line.Single, w = Line.Single });
                    DrawBox(left, top + r.Height, new BoxGlyph { n = Line.Double, w = Line.Single, e = Line.Single });
                } else {
                    DrawBox(left, top, new BoxGlyph { n = Line.Single, e = Line.Single, s = Line.Single, w = Line.Single });
                }

                void DrawCornerNW() {
                    //Left top
                    DrawBox(left, top, new BoxGlyph { e = Line.Single, s = Line.Single });
                }
                void DrawCornerNE() {
                    //Right top
                    DrawBox(left + r.Width, top, new BoxGlyph { w = Line.Single, s = Line.Single });
                }
                void DrawCornerSW() {
                    //Left bottom
                    DrawBox(left, top + r.Height, new BoxGlyph { e = Line.Single, n = Line.Single });
                }
                void DrawCornerSE() {
                    //Right bottom
                    DrawBox(left + r.Width, top + r.Height, new BoxGlyph { n = Line.Single, w = Line.Single });
                }
                void DrawSidesX(Line style = Line.Single) {
                    foreach (var x in Enumerable.Range(left, r.Width)) {
                        DrawBox(x, top, new BoxGlyph { e = style, w = style });
                        DrawBox(x, top + r.Height, new BoxGlyph { e = style, w = style });
                    }
                }
                void DrawSidesY(Line style = Line.Single) {
                    foreach (var y in Enumerable.Range(top, r.Height)) {
                        DrawBox(left, y, new BoxGlyph { n = style, s = style });
                        DrawBox(left + r.Width, y, new BoxGlyph { n = style, s = style });
                    }
                }
            }
            void DrawBox(int x, int y, BoxGlyph g) {
                this.SetCellAppearance(x, y, new ColoredGlyph(model.brush.foreground, model.brush.background, BoxInfo.IBMCGA.glyphFromInfo[g]));
            }
        }
        public override bool ProcessKeyboard(Keyboard info) {
            if(info.IsKeyPressed(S) && info.IsKeyDown(LeftControl)) {
                //File.WriteAllText(Path.Combine(Environment.CurrentDirectory, Path.GetFileName(Path.GetTempFileName())), JsonConvert.SerializeObject(model));
                SadConsole.Game.Instance.Screen = (new SaveMenu(Width, Height, model));
            }
            model.ProcessKeyboard(info);
            return base.ProcessKeyboard(info);
        }
        public override bool ProcessMouse(MouseScreenObjectState state) {
            IsFocused = true;
            model.ProcessMouse(state, IsMouseOver);
            return base.ProcessMouse(state);
        }
    }
    enum Mode {
        Edit, Select, Keyboard
    }
    class SpriteModel {
        int width, height;

        Stack<SingleEdit> Undo;
        Stack<SingleEdit> Redo;

        public string filepath;
        public Sprite sprite = new Sprite(16, 16);

        public TileModel tiles;
        public BrushMode brush;
        public KeyboardMode keyboard;
        public SelectMode select;
        public PanMode pan;

        public int ticks = 0;
        public int ticksSelect = 0;

        public Point camera = new Point();
        public Point cursor = new Point();       //Position on the image; stored as offset from camera
        public Point cursorScreen = new Point();
        public bool keyboardMode;

        public Point prevCell;
        public bool prevLeft;

        public Mode mode;

        public SpriteModel(int width, int height) {
            this.width = width;
            this.height = height;
            tiles = new TileModel();
            brush = new BrushMode(this);
            keyboard = new KeyboardMode(this);
            select = new SelectMode(this);
            pan = new PanMode(this);
            Undo = new Stack<SingleEdit>();
            Redo = new Stack<SingleEdit>();
            mode = Mode.Edit;
        }
        public bool IsEditable(Point p) {
            bool result = sprite.InBounds(p);
            if(select.rect.HasValue) {
                var r = select.rect.Value;
                result = result && p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
                
            }
            return result;
        }
        public void ProcessKeyboard(Keyboard info) {
            if (info.IsKeyDown(LeftControl) && info.IsKeyUp(LeftShift) && info.IsKeyPressed(Z)) {
                if (Undo.Any()) {
                    var u = Undo.Pop();
                    u.Undo();
                    Redo.Push(u);
                }
                return;
            }
            if (info.IsKeyDown(LeftControl) && info.IsKeyDown(LeftShift) && info.IsKeyPressed(Z)) {
                if (Redo.Any()) {
                    var u = Redo.Pop();
                    u.Do();
                    Undo.Push(u);
                }
                return;
            }
            if (mode == Mode.Keyboard) {
                if(info.IsKeyPressed(Escape)) {
                    mode = Mode.Edit;
                } else {
                    keyboard.ProcessKeyboard(info);
                }
            } else {
                if (info.IsKeyPressed(Space)) {
                    if(!pan.allowPan) {
                        pan.allowPan = true;
                        pan.startPan = cursor;
                    }
                } else if (info.IsKeyReleased(Space)) {
                    pan.allowPan = false;
                    camera -= pan.offsetPan;
                    pan.startPan = new Point(0, 0);
                    pan.offsetPan = new Point(0, 0);
                } else if(info.IsKeyPressed(T)) {
                    mode = Mode.Keyboard;
                    keyboard.keyCursor = null;
                    //keyboard.keyCursor = cursor;
                    //keyboard.margin = cursor;
                } else if(info.IsKeyPressed(S) && !info.IsKeyDown(LeftControl)) {
                    mode = Mode.Select;
                }

                if (pan.allowPan) {
                    pan.ProcessKeyboard(info);
                }
            }
        }
        public void ProcessMouse(MouseScreenObjectState state, bool IsMouseOver) {
            cursorScreen = state.SurfaceCellPosition;
            cursor = cursorScreen + camera;
            if (pan.allowPan) {
                pan.ProcessMouse(state);
            } else {
                switch (mode) {
                    case Mode.Edit:
                        brush.ProcessMouse(state, IsMouseOver);
                        break;
                    case Mode.Select:
                        select.ProcessMouse(state);
                        break;
                    case Mode.Keyboard:
                        keyboard.ProcessMouse(state);
                        break;
                }
            }


            prevCell = cursor;
            prevLeft = state.Mouse.LeftButtonDown;
        }
        public void AddAction(SingleEdit edit) {
            if(edit.IsRedundant()) {
                return;
            }
            Undo.Push(edit);
            Redo.Clear();
            edit.Do();
        }
    }
    interface Edit {
        void Undo();
        void Do();
    }
    class SingleEdit {
        public Point cursor;
        public ILayer layer;
        public TileRef prev;
        public TileRef next;
        public SingleEdit(Point cursor, ILayer layer, TileRef next) {
            this.cursor = cursor;
            this.layer = layer;
            this.prev = layer[cursor];
            this.next = next;
        }
        public void Undo() {
            layer[cursor] = prev;
        }
        public void Do() {
            layer[cursor] = next;
        }
        public bool IsRedundant() => prev.Background == next.Background && prev.Foreground == next.Foreground && prev.Glyph == next.Glyph;
    }
    class PanMode {
        SpriteModel model;
        public bool allowPan;
        public Point startPan;     //Position on screen where user held down space and left clicked
        public Point offsetPan;

        public PanMode(SpriteModel model) {
            this.model = model;
        }
        public void ProcessKeyboard(Keyboard info) {
            if (info.IsKeyPressed(Right)) {
                startPan += new Point(-1, 0);
            }
            if (info.IsKeyPressed(Left)) {
                startPan += new Point(1, 0);
            }
            if (info.IsKeyPressed(Up)) {
                startPan += new Point(0, 1);
            }
            if (info.IsKeyPressed(Down)) {
                startPan += new Point(0, -1);
            }
            UpdateOffset();
        }
        public void ProcessMouse(MouseScreenObjectState state) {
            UpdateOffset();
        }
        public void UpdateOffset() {
            offsetPan = model.cursor - (Point)startPan;
        }
    }
    class BrushMode {
        SpriteModel model;
        public MouseWatch mouse;
        public int glyph = 'A';
        public Color foreground = Color.Red;
        public Color background = Color.Black;
        public TileValue cell {
            get => new TileValue(foreground, background, glyph); set {
                foreground = value.Foreground;
                background = value.Background;
                glyph = value.Glyph;
            }
        }
        public BrushMode(SpriteModel model) {
            this.model = model;
            mouse = new MouseWatch();
        }

        public void ProcessMouse(MouseScreenObjectState state, bool IsMouseOver) {
            mouse.Update(state, IsMouseOver);
            glyph = (char)((glyph + state.Mouse.ScrollWheelValueChange / 120 + 255) % 255);
            if(state.IsOnScreenObject) {
                if (state.Mouse.LeftButtonDown && mouse.leftPressedOnScreen) {
                    var prev = model.prevCell;
                    var offset = (model.cursor - prev);
                    var length = offset.Length();
                    for (int i = 0; i < length; i++) {

                        var p = prev + new Point((int)(i * offset.X / length), (int)(i * offset.Y / length));
                        if (model.IsEditable(p)) {
                            Place(p);
                        }
                    }

                    if (model.IsEditable(model.cursor)) {
                        Place(model.cursor);
                    }
                }
            }
        }
        void Place(Point p) {
            var layer = model.sprite.layers[0];
            SingleEdit action;
            if(model.tiles.brushIndex.HasValue) {
                action = new SingleEdit(p, layer, model.tiles.brushTile);
            } else {
                action = new SingleEdit(p, layer, model.brush.cell);
            }
            
            model.AddAction(action);
        }
    }
    class KeyboardMode {
        SpriteModel model;
        public Point? keyCursor;
        public Point margin;

        public KeyboardMode(SpriteModel model) {
            this.model = model;
            keyCursor = new Point();
        }
        public void ProcessKeyboard(Keyboard info) {
            ref int ticks = ref model.ticks;
            if (info.IsKeyPressed(Right)) {
                keyCursor = (keyCursor ?? model.cursor) + new Point(1, 0);
                ticks = 0;
            }
            if (info.IsKeyPressed(Left)) {
                keyCursor = (keyCursor ?? model.cursor) + new Point(-1, 0);
                ticks = 0;
            }
            if (info.IsKeyPressed(Up)) {
                keyCursor = (keyCursor ?? model.cursor) + new Point(0, -1);
                ticks = 0;
            }
            if (info.IsKeyPressed(Down)) {
                keyCursor = (keyCursor ?? model.cursor) + new Point(0, 1);
                ticks = 0;
            }
            if(info.IsKeyPressed(Enter) && keyCursor.HasValue) {
                keyCursor = new Point(margin.X, keyCursor.Value.Y + 1);
                ticks = 0;
            }

            ref var sprite = ref model.sprite;
            ref var brush = ref model.brush;
            var pressed = info.KeysPressed.Where(k => k.Character != 0);
            if (pressed.Any()) {
                char c = pressed.First().Character;
                var p = keyCursor ?? model.cursor;
                if(model.IsEditable(p)) {
                    if (c != 0) {
                        var layer = sprite.layers[0];
                        var tile = new TileValue(brush.foreground, brush.background, c);
                        var action = new SingleEdit(p, layer, tile);
                        model.AddAction(action);
                        ticks = 15;
                        //keyCursor += new Point(1, 0);
                    }
                }

            } else if (info.IsKeyPressed(Back)) {
                var layer = sprite.layers[0];
                var p = keyCursor ?? model.cursor;
                var action = new SingleEdit(p, layer, new TileValue(Color.Transparent, Color.Transparent, ' '));
                model.AddAction(action);
                ticks = 15;

            }
        }
        public void ProcessMouse(MouseScreenObjectState state) {
            if(state.IsOnScreenObject) {
                if (state.Mouse.LeftButtonDown) {
                    keyCursor = margin = model.cursor;
                }
            }
        }
    }
    class SelectMode {
        SpriteModel model;
        public Point? start;
        Point end;
        public Rectangle? rect;
        bool prevLeft;

        public SelectMode(SpriteModel model) {
            this.model = model;
        }
        public void ProcessMouse(MouseScreenObjectState state) {
            if(state.IsOnScreenObject) {
                if (state.Mouse.LeftButtonDown) {

                    if (prevLeft) {
                        if (end != model.cursor) {
                            end = model.cursor;

                            int leftX = Math.Min(start.Value.X, end.X);
                            int width = Math.Max(start.Value.X, end.X) - leftX;
                            int topY = Math.Min(start.Value.Y, end.Y);
                            int height = Math.Max(start.Value.Y, end.Y) - topY;
                            rect = new Rectangle(leftX, topY, width, height);

                            //var t = model.ticks % 30;
                            //model.ticksSelect = t < 15 ? t - 15 : t - 30;
                            model.ticksSelect = -15;
                        }
                    } else {
                        start = model.cursor;
                        rect = new Rectangle(start.Value, new Point(0, 0));
                    }
                } else if (prevLeft) {
                    if (start == end) {
                        rect = null;
                    }
                }
                prevLeft = state.Mouse.LeftButtonDown;
            }
            
        }
        public bool GetAdjustedRect(out Rectangle r) {
            if(rect.HasValue) {
                r = rect.Value.Translate(new Point() - model.camera);
            } else {
                r = Rectangle.Empty;
            }
            return rect.HasValue;
        }
    }
}