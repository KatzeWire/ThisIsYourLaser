﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class buildingParameters : MonoBehaviour {
    [HideInInspector]
    public int x = -1;
    [HideInInspector]
    public int y = -1;
    [HideInInspector]
    public Player owner = Player.World;

    public Building building = Building.Empty;
    public float health = 100;
    public float cost = 100;

}
