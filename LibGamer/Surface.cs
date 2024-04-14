﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LibGamer;
public interface ISurf {
	int Width { get; }
	int Height { get; }
	public Grid<uint> Front { get; }
	public Grid<uint> Back { get; }
	public Grid<Tile> Tile { get; }
	public void Clear ();
	public uint GetFront (int x, int y);
	public void SetFront (int x, int y, uint color);
	public uint GetBack (int x, int y);
	public void SetBack (int x, int y, uint color);
	public uint GetGlyph (int x, int y);
	public void SetGlyph (int x, int y, uint g);
	public Tile GetTile (int x, int y);
	public void SetTile (int x, int y, Tile g);

	public void Print (int x, int y, params Tile[] t);
	public record Grid<T>(Grid<T>.Get get, Grid<T>.Set set) {
		public delegate T Get(int x, int y);
		public delegate void Set (int x, int y, T t);
		public T this[int x, int y] {
			get => get(x, y);
			set => set(x, y, value);
		}
		public static implicit operator Grid<T> ((Get get, Set set) t) => new(t.get, t.set);
	}


}
