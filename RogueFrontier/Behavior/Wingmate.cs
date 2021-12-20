﻿using Common;
using Newtonsoft.Json;

namespace RogueFrontier;

public enum WingOrder {
    Escort,
    Wait,
    BreakAndAttack,
    Scout,
}
public class Wingmate : IShipBehavior, IContainer<PlayerShip.Destroyed> {
    public PlayerShip player;
    public IShipOrder order;

    [JsonIgnore]
    public PlayerShip.Destroyed Value => (s, d, w) => {
        order = new AttackOrder(d);
    };

    //This class handles orders and communications
    public Wingmate(PlayerShip player) {
        this.player = player;
    }
    public void Update(AIShip owner) {
        if(order?.Active != true) {
            order = new EscortOrder(player, new());
        }
        order?.Update(owner);
    }
}
