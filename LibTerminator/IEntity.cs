﻿using Common;
using LibGamer;
using System;
namespace LibTerminator;
public interface IEntity {
	XYI pos { get; }
	Tile tile { get; }
	Action Removed { get; set; }
}
public interface IActor {
	Action[] UpdateTick () { return []; }
	void UpdateReal (TimeSpan delta) { }
}