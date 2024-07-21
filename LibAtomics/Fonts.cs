﻿using LibGamer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibAtomics;
public static class Fonts {
#if GODOT
	public static string ROOT = "Lib/LibFrontier/Assets";
#else
	public static string ROOT = "Assets";
#endif
	public static string IBMCGA_8x8_JSON { get; } = $"{ROOT}/font/IBMCGA+_8x8.font";
	public static string IBMCGA_6x8_JSON { get; } = $"{ROOT}/font/IBMCGA+_6x8.font";
	public static string RF_8x8_JSON { get; } = $"{ROOT}/font/RF_8x8.font";
	public static Tf IBMCGA_8x8 { get; } = new Tf(File.ReadAllBytes($"{ROOT}/font/IBMCGA+_8x8.png"), "IBMCGA+_8x8", 8, 8, 256 / 8, 256 / 8, 219);
	public static Tf IBMCGA_6x8 { get; } = new Tf(File.ReadAllBytes($"{ROOT}/font/IBMCGA+_6x8.png"), "IBMCGA+_6x8", 6, 8, 192 / 6, 64 / 8, 219);
	public static Tf RF_8x8 { get; } = new Tf(File.ReadAllBytes($"{ROOT}/font/RF_8x8.png"), "RF_8x8", 8, 8, 256 / 8, 256 / 8, 219);
}