﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

// README: ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//                                                                                                                                  //
//  Grid coordinates start from the bottom left corner. IE: Increasing X values go right, increasing Y values go up.                //
//  Here is a list of accessible methods:                                                                                           //
//                                                                                                                                  //
//  getCellInfo(int x, int y)                                               - returns GridItem at specified coords.                 //
//  placeBuilding(int x, int y, Building newBuilding, Player playerID)      - Places a building into theGrid, rotation optional.    //
//  removeBuilding(int x, int y, Player playerID)                           - Removes a building, returns half of its resources.    //
//  destroyBuilding(int x, int y)                                           - Removes a building from theGrid.                      //
//  moveBuilding(int x, int y, int xNew, int yNew, Player playerID)         - Moves a building to a new location.                   //
//  upgradeBuilding(int x, int y, Player playerID)                          - Upgrades a building if it can be upgraded.            //
//                                                                                                                                  //
//  *** The above functions will return true if the succeed, or false if they fail.                                                 //
//  *** Functions are called like this: gridManager.theGrid.placeBuilding(x, y, Building.Blocking, Player.PlayerOne);               //
//  *** The player parameter should be the player trying to perform the action. IE: PlayerOne if PlayerOne is placing a building.   //
//                                                                                                                                  //
//  Example:                                                                                                                        //
//  if (gridManager.theGrid.placeBuilding(x, y, Building.Blocking, Player.PlayerOne, Direction.Up)) {                               //
//      print("Success");                                                                                                           //
//  } else {                                                                                                                        //
//      print("Failed to place building, make sure you select a valid location");                                                   //
//  }                                                                                                                               //
//                                                                                                                                  //
//  Example2:                                                                                                                       //
//  print(gridManager.theGrid.getCellInfo(x, y).toString()); // Use this for debugging grid cells.                                  //
//                                                                                                                                  //
//  Example3:                                                                                                                       //
//  foreach (Transform building in gridManager.theGrid.getBuildingContainer().transform)    // Print the health of all buildings    //
//      print(building.GetComponent<buildingParameters>().currentHP);                                                               //
//                                                                                                                                  //
//                                                                                                                                  //
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public enum Building { Base, Laser, Blocking, Reflecting, Refracting, Redirecting, Resource, Portal, Empty };
public enum Player { World, PlayerOne, PlayerTwo, Shared }; // World Refers to neutral spaces owned by neither player
public enum Direction { None, NE, NW, SE, SW, Left, Right, Up, Down };

[Serializable]
public struct XY
{
    public int x, y;
    public XY(int X, int Y)
    {
        x = X;
        y = Y;
    }
    public bool Equals(XY other)
    {
        return x == other.x && y == other.y;
    }
    public static bool operator ==(XY a, XY b) {
        return a.Equals(b);
    }
    public static bool operator !=(XY a, XY b)
    {
        return !(a == b);
    }
}

public class buildingRequest
{
    public XY coords;
    public float time;
    public Building building;
    public Player owner;
    public Direction direction;
    public float health;

    public buildingRequest(XY xy, float actionTime, Building structure = Building.Empty, Player ownedBy = Player.World, Direction facing = Direction.None, float hitpoints = 0f)
    {
        coords = xy;
        building = structure;
        time = actionTime;
        owner = ownedBy;
        direction = facing;
        health = hitpoints;
    }

    public float updateTime(float delta)
    {
        time -= delta; return time;
    }
}

public struct GridItem
{
    public bool isEmpty;        // Empty grid cell?
    public Building building;   // Building type
    public Player owner;        // Who owns the block
    public Direction direction; // Used for laser block direction, other block rotations
    public byte level;          // Upgrade level
    public float health;
    public bool markedForDeath;

    public GridItem(bool emptyCell, Building structure, Player ownedBy, Direction facingDirection, float hitpoints, bool dying = false)
    {
        isEmpty = emptyCell;
        building = structure;
        level = 0;
        owner = ownedBy;
        direction = facingDirection;
        health = hitpoints;
        markedForDeath = dying;
    }

    public string toString()    // Convert GridItem to string for easy printing
    {
        return "isEmpty: " + isEmpty + "  |  Building: " + building + "  |  Level: " + level + "  |  HP: " + health + "  |  Direction: " + direction + "  |  Owner: " + owner;
    }
}

public struct Grid
{
    public GridItem[,] grid;
    public GridItem[,] gridDots;
    public List<buildingRequest> placementList;
    public List<buildingRequest> removalList;
    public List<buildingRequest> destructionList;
    private GameObject buildingContainer;
    private GameObject[] buildingPrefabs;
    public Dictionary<XY, GameObject> prefabDictionary;
    private float resourceLimit;
    private int dimX;
    private int dimY;
    private float resourcesP1;
    private float resourcesP2;
    private float buildingNumP1;
    private float buildingNumP2;
    private float timer;
    private float hitTimer;
    private float laserCeiling;
    private bool needsUpdate;
    public GameObject tutorialObject;
    private GameObject baseP1;
    private GameObject baseP2;
    private GameObject placementTimer;
    public GameObject[,] gridSquares;
    public int blockScale;
   
    public Grid(int x, int y, GameObject container, GameObject basePrefab, GameObject basePrefab2, GameObject laserPrefab, GameObject laserPrefab2, GameObject blockPrefab, GameObject blockPrefab2,
        GameObject reflectPrefab, GameObject reflectPrefab2, GameObject refractPrefab, GameObject refractPrefab2, GameObject redirectPrefab, GameObject redirectPrefab2, GameObject resourcePrefab,
        GameObject resourcePrefab2, GameObject portalPrefab, GameObject portalPrefab2, float resources, float buildings, GameObject emptyHolder,
        GameObject Dots, GameObject placementTimerObj, GameObject tutorial, float limit, int blockResourceScale)
    {
        grid = new GridItem[y, x];
        // Generate Object Holder
        GameObject trashCompactor = new GameObject();
        trashCompactor.name = "ObjectHolder";
        trashCompactor.transform.SetParent(container.transform.parent.transform);
        gridSquares = new GameObject[y, x];
        for (int row = 0; row < y; row++) {
            for (int col = 0; col < x; col++) {
                grid[row, col] = new GridItem(true, Building.Empty, Player.World, Direction.None, 0);
                GameObject empty = MonoBehaviour.Instantiate(emptyHolder);    //makes transparent planes on each grid square
                empty.transform.SetParent(trashCompactor.transform);
                empty.transform.localPosition = new Vector3((-x / 2) + col + 0.5f, -0.1f, (-y / 2) + row + 0.5f);
                Color gridColor = Color.white;
                //if (col < (x/2)) {
                //    gridColor = Color.red;
                //}
                //else {
                //    gridColor = Color.green;
                //}
                gridColor.a = 0.20f;
                empty.GetComponent<Renderer>().material.color = gridColor;
                gridSquares[row, col] = empty;
            }
        }

        gridDots = new GridItem[y, x];
        for (int row = 0; row < y; row++) {
            for (int col = 0; col < x-1; col++) {
                GameObject dot = MonoBehaviour.Instantiate(Dots);
                dot.transform.SetParent(trashCompactor.transform);
                dot.transform.localPosition = new Vector3((-x / 2) + col + 1f, -0.1f, (-y / 2) + row + 0.5f);
            }
        }
        for (int row = 0; row < y-1; row++) {
            for (int col = 0; col < x; col++) {
                GameObject dot = MonoBehaviour.Instantiate(Dots);
                dot.transform.SetParent(trashCompactor.transform);
                dot.transform.localPosition = new Vector3((-x / 2) + col + 0.5f, -0.1f, (-y / 2) + row + 1f);
            }
        }

        dimX = x;
        dimY = y;
        buildingContainer = container;
        buildingPrefabs = new GameObject[16]; // 8 Buildings x 2 Players
        buildingPrefabs[(int)Building.Base] = basePrefab;
        buildingPrefabs[(int)Building.Laser] = laserPrefab;
        buildingPrefabs[(int)Building.Blocking] = blockPrefab;
        buildingPrefabs[(int)Building.Reflecting] = reflectPrefab;
        buildingPrefabs[(int)Building.Refracting] = refractPrefab;
        buildingPrefabs[(int)Building.Redirecting] = redirectPrefab;
        buildingPrefabs[(int)Building.Resource] = resourcePrefab;
        buildingPrefabs[(int)Building.Portal] = portalPrefab;
        buildingPrefabs[(int)Building.Base + 8] = basePrefab2;
        buildingPrefabs[(int)Building.Laser + 8] = laserPrefab2;
        buildingPrefabs[(int)Building.Blocking + 8] = blockPrefab2;
        buildingPrefabs[(int)Building.Reflecting + 8] = reflectPrefab2;
        buildingPrefabs[(int)Building.Refracting + 8] = refractPrefab2;
        buildingPrefabs[(int)Building.Redirecting + 8] = redirectPrefab2;
        buildingPrefabs[(int)Building.Resource + 8] = resourcePrefab2;
        buildingPrefabs[(int)Building.Portal + 8] = portalPrefab2;
        prefabDictionary = new Dictionary<XY, GameObject>(buildingPrefabs.Length);
        placementList = new List<buildingRequest>();
        removalList = new List<buildingRequest>();
        destructionList = new List<buildingRequest>();
        placementTimer = placementTimerObj;
        resourcesP1 = resources;
        resourcesP2 = resources;
        buildingNumP1 = buildings;
        buildingNumP2 = buildings;
        baseP1 = null;
        baseP2 = null;
        needsUpdate = false;
        resourceLimit = limit;
        tutorialObject = tutorial;
        blockScale = blockResourceScale;
        timer = .5f;
        hitTimer = 4.5f;
        laserCeiling = 1f;
    }

    private bool validateInput(int x, int y)
    {
        if (x < 0 || y < 0 || x >= dimX || y >= dimY) return false;
        return true;
    }

    private Vector3 directionToEular(Direction direction) // obsolete?
    {
        switch (direction) {
            case Direction.Right: return new Vector3(90, 90, 0);
            case Direction.Down: return new Vector3(90, 180, 0);
            case Direction.Left: return new Vector3(90, 270, 0);
        }
        return new Vector3(90, 0, 0);
    }

    public int directionToIndex(Direction direction)
    {
        switch (direction) {
            case Direction.Right: return 1;
            case Direction.Down: return 3;
            case Direction.Left: return 0;
        }
        return 2;
    }


    // Determine if other buildings have weaksides that should block your building's placement
    // Modify this and the below function if you want to change weaksides for structure placement.
    // Note weak sides for laser logic are handled seperatly in their respective laser logic functions.
    private bool isWeakSide(int x, int y, Direction dir, Building structure)
    {
        // Buildings with no weak sides
        if (structure == Building.Blocking) return false;
        // Buildings with all weak sides
        else if (structure == Building.Base || structure == Building.Laser || structure == Building.Refracting) return true;
        else if (structure == Building.Reflecting) return getDirection(x, y) == dir || getDirection(x, y) == ghostLaser.opposite(dir);
        else if (structure == Building.Resource) return getDirection(x, y) == dir;
        else if (structure == Building.Redirecting) {
            if (getDirection(x, y) == Direction.Up || getDirection(x, y) == Direction.Down) { if (dir == Direction.Right || dir == Direction.Left) return true; }
            else { if (dir == Direction.Up || dir == Direction.Down) return true; }
        }
        return false;
    }
    // Used for determining if the building you are placing has a weakside that will intersect with existing buildings
    private bool isWeakSide(Direction dir, Direction placeDir, Building structure)
    {
        if (dir == Direction.None) return false;
        // Buildings with no weak sides
        if (structure == Building.Blocking) return false;
        // Buildings with all weak sides
        else if (structure == Building.Base || structure == Building.Laser || structure == Building.Refracting) return true;
        else if (structure == Building.Reflecting) return placeDir == dir || placeDir == ghostLaser.opposite(dir);
        else if (structure == Building.Resource) return placeDir == dir;
        else if (structure == Building.Redirecting && placeDir != Direction.None) {
            if (placeDir == Direction.Up || placeDir == Direction.Down) { if (dir == Direction.Right || dir == Direction.Left) return true; } else { if (dir == Direction.Up || dir == Direction.Down) return true; }
        }
        return false;
    }

    // Checks if your placement is valid and does not intersect with other weaksides
    public bool probeGrid(int x, int y, Direction placeDir, Building structure, int originX = -1, int originY = -1)
    {
        if (!validateInput(x, y)) return false;
        if (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y)) && (x != originX && y != originY)) return false;
        if (!(x == originX && y - 1 == originY) && (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y - 1)) || getBuilding(x, y - 1) != Building.Empty) && (isWeakSide(x, y - 1, Direction.Up, getBuilding(x, y - 1)) || isWeakSide(Direction.Down, placeDir, structure))) return false;
        if (!(x == originX && y + 1 == originY) && (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y + 1)) || getBuilding(x, y + 1) != Building.Empty) && (isWeakSide(x, y + 1, Direction.Down, getBuilding(x, y + 1)) || isWeakSide(Direction.Up, placeDir, structure))) return false;
        if (!(x - 1 == originX && y == originY) && (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x - 1, y)) || getBuilding(x - 1, y) != Building.Empty) && (isWeakSide(x - 1, y, Direction.Right, getBuilding(x - 1, y)) || isWeakSide(Direction.Left, placeDir, structure))) return false;
        if (!(x + 1 == originX && y == originY) && (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x + 1, y)) || getBuilding(x + 1, y) != Building.Empty) && (isWeakSide(x + 1, y, Direction.Left, getBuilding(x + 1, y)) || isWeakSide(Direction.Right, placeDir, structure))) return false;
        if (structure == Building.Laser && (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y - 2)) || getBuilding(x, y - 2) != Building.Empty) && (getBuilding(x, y - 2) == Building.Base)) return false;
        if (structure == Building.Laser && (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y + 2)) || getBuilding(x, y + 2) != Building.Empty) && (getBuilding(x, y + 2) == Building.Base)) return false;
        return true;
    }

    public float getCost(Building building, int x = -1, Player player = Player.World, bool moving = false, bool removing = false, bool swaping = false)
    {
        if (TutorialFramework.tutorialActive) return 0f;
        if (building == Building.Empty) return 0f;
        float cost = buildingPrefabs[(int)building].GetComponent<buildingParameters>().cost;
        if (x == -1) return cost;
        if (player == Player.PlayerOne && x >= gridManager.theGrid.getDimX() / 2 + 5) cost *= 10f;
        else if (player == Player.PlayerOne && x >= gridManager.theGrid.getDimX() / 2 + 4) cost *= 4f;
        else if (player == Player.PlayerOne && x >= gridManager.theGrid.getDimX() / 2 + 3) cost *= 3.5f;
        else if (player == Player.PlayerOne && x >= gridManager.theGrid.getDimX() / 2 + 2) cost *= 3f;
        else if (player == Player.PlayerOne && x >= gridManager.theGrid.getDimX() / 2 + 1) cost *= 2.5f;
        else if (player == Player.PlayerOne && x >= gridManager.theGrid.getDimX() / 2) cost *= 2f;
        else if (player == Player.PlayerTwo && x < gridManager.theGrid.getDimX() / 2 - 5) cost *= 10f;
        else if (player == Player.PlayerTwo && x < gridManager.theGrid.getDimX() / 2 - 4) cost *= 4f;
        else if (player == Player.PlayerTwo && x < gridManager.theGrid.getDimX() / 2 - 3) cost *= 3.5f;
        else if (player == Player.PlayerTwo && x < gridManager.theGrid.getDimX() / 2 - 2) cost *= 3f;
        else if (player == Player.PlayerTwo && x < gridManager.theGrid.getDimX() / 2 - 1) cost *= 2.5f;
        else if (player == Player.PlayerTwo && x < gridManager.theGrid.getDimX() / 2) cost *= 2f;
        if (moving || removing) cost /= 2f;
        else if (swaping) cost /= 4f;
        return cost;
    }

    private bool canRotate(Building building) // Add buildings here that support 4 way sprite rotation
    {
        if (building == Building.Blocking || building == Building.Reflecting || building == Building.Refracting || building == Building.Resource)
            return true;
        return false;
    }

    public GridItem getCellInfo(int x, int y) { return validateInput(x, y) ? grid[y, x] : new GridItem(true, Building.Empty, Player.World, Direction.None, 0); }
    public Building getBuilding(int x, int y) { if (!validateInput(x, y)) return Building.Empty; if (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y))) return gridManager.theGrid.prefabDictionary[new XY(x, y)].GetComponent<buildingParameters>().buildingType; return grid[y, x].building; }
    public Player getOwner(int x, int y) { return validateInput(x, y) ? grid[y, x].owner : Player.World; }
    public Direction getDirection(int x, int y) { if (!validateInput(x, y)) return Direction.None; if (gridManager.theGrid.prefabDictionary.ContainsKey(new XY(x, y))) return gridManager.theGrid.prefabDictionary[new XY(x, y)].GetComponent<buildingParameters>().direction; return grid[y, x].direction; }
    public int getDimX() { return dimX; }
    public int getDimY() { return dimY; }
    public float getResourcesP1() { return resourcesP1; }
    public float getResourcesP2() { return resourcesP2; }
    public float baseHealthP1() { return baseP1 != null ? baseP1.GetComponent<buildingParameters>().currentHP : 0f; }
    public float baseHealthP2() { return baseP2 != null ? baseP2.GetComponent<buildingParameters>().currentHP : 0f; }
    public void addResources(float p1, float p2) { resourcesP1 += p1; resourcesP2 += p2; if (resourcesP1 >= resourceLimit) resourcesP1 = resourceLimit; if (resourcesP2 >= resourceLimit) resourcesP2 = resourceLimit; }
    public bool updateLaser() { return needsUpdate; }
    public void updateFinished() { needsUpdate = false; }
    public void queueUpdate() { needsUpdate = true; }

    public GameObject getBuildingContainer() { return buildingContainer; }
    public Vector3 coordsToWorld(float x, float y, float yOffset = 0f) // Use this to easily convert coords from grid space to world space
    {
        return new Vector3(x - (dimX / 2f) + 0.5f, yOffset, y - (dimY / 2f) + 0.5f);
    }

    public void updateSquares()
    {
        for (int i = 0; i < gridSquares.GetLength(0); i++) {
            for (int j = 0; j < gridSquares.GetLength(1); j++) {
                if (probeGrid(j, i, Direction.Up, Building.Blocking)) gridSquares[i, j].GetComponent<Renderer>().enabled = true;
                else gridSquares[i, j].GetComponent<Renderer>().enabled = false;
            }
        }
    }

    public bool applyDamage(int x, int y, float damage)
    {
        if (!validateInput(x, y)) return false;
        if (!grid[y, x].isEmpty) {
            grid[y, x].health -= damage;
            timer -= Time.deltaTime*5;
            hitTimer -= Time.deltaTime*5;
            if(timer < 0 ){
                if(((float)(Math.Exp( 1/(grid[y, x].health))-1)/(float)(Math.E-1)) < laserCeiling){

                    SoundManager.PlaySound(inputController.Sounds[5].audioclip,SoundManager.globalSoundsVolume*((float)(Math.Exp( 1/(grid[y, x].health))-1)/(float)(Math.E-1))/80);
                    timer = .7f;
                    MonoBehaviour.print(SoundManager.globalSoundsVolume*((float)(Math.Exp( 1/(grid[y, x].health))-1)/(float)(Math.E-1))/80);
                }
                else{
                    SoundManager.PlaySound(inputController.Sounds[5].audioclip,SoundManager.globalSoundsVolume*laserCeiling/80); 
                    timer = .7f;
                    MonoBehaviour.print(SoundManager.globalSoundsVolume*laserCeiling/80);
                }
            }
            if(hitTimer < 0 ){
                SoundManager.PlaySound(inputController.Sounds[6].audioclip,SoundManager.globalSoundsVolume/60); 
                hitTimer = 4.5f;
            }

            MonoBehaviour.print(SoundManager.globalSoundsVolume*((float)(Math.Exp( 1/(grid[y, x].health))-1)/(float)(Math.E-1)));
            prefabDictionary[new XY(x, y)].GetComponent<buildingParameters>().currentHP = grid[y, x].health;
            floatingNumbers.floatingNumbersStruct.checkDamage(new XY(x, y), grid[y, x].health, prefabDictionary[new XY(x, y)].GetComponent<buildingParameters>().health, grid[y, x].building, grid[y, x].owner);
            if (grid[y, x].health <= 0f) {
                if (tutorialObject != null && TutorialFramework.tutorialActive) {
                    tutorialObject.GetComponent<TutorialFramework>().buildingDestructionEvent(new XY(x, y), grid[y, x].building);
                } else {
                    if (getBuilding(x, y) == Building.Base && baseHealthP2() <= 0f) SceneManager.LoadScene("P1Win", LoadSceneMode.Single);
                    else if (getBuilding(x, y) == Building.Base && baseHealthP1() <= 0f) SceneManager.LoadScene("P2Win", LoadSceneMode.Single);
                }
                destroyBuilding(x, y);
            }
        } else return false;
        return true;
    }

    public bool placeBuilding (int x, int y, Building newBuilding, Player playerID, Direction facing = Direction.Up, bool instant = false)
	{
		if (!validateInput (x, y))
			return false;
        if (grid [y, x].isEmpty && probeGrid (x, y, facing, newBuilding) && newBuilding != Building.Empty && (playerID == Player.PlayerOne ? resourcesP1 : resourcesP2) >= getCost (newBuilding) && (playerID == Player.PlayerOne ? buildingNumP1 : buildingNumP2) <= 11) { //10 buildings per player
			if (prefabDictionary.ContainsKey (new XY (x, y)))
				return false;
            // Place Building Prefab
            GameObject building = MonoBehaviour.Instantiate(buildingPrefabs [(int)newBuilding + (playerID == Player.PlayerOne ? 0 : 8)]);
			if (instant) placementList.Add (new buildingRequest(new XY (x, y), 0f, newBuilding, playerID, facing, building.GetComponent<buildingParameters>().health)); // ADD BUILDING TO DELAYED BUILD LIST with a time of 0 (instant placement)
			else placementList.Add (new buildingRequest(new XY (x, y), building.GetComponent<buildingParameters>().placementTime, newBuilding, playerID, facing, building.GetComponent<buildingParameters>().health)); // ADD BUILDING TO DELAYED BUILD LIST
			building.GetComponent<buildingParameters>().x = x;
			building.GetComponent<buildingParameters>().y = y;
			building.GetComponent<buildingParameters>().owner = playerID;
			building.GetComponent<buildingParameters>().direction = facing;
			building.GetComponent<buildingParameters>().buildingType = newBuilding;
            building.GetComponent<buildingParameters>().currentHP = building.GetComponent<buildingParameters>().health;
            if (newBuilding != Building.Laser && newBuilding != Building.Base) {// || newBuilding == Building.Blocking || newBuilding == Building.Resource || newBuilding == Building.Blocking) { // This if statement will be removed once all buildings are set up properly
                building.AddComponent<SpriteRenderer>();
                building.GetComponent<SpriteRenderer>().sprite = building.GetComponent<buildingParameters>().sprites[directionToIndex(facing)];
                building.GetComponent<Renderer>().material.color = playerID == Player.PlayerOne ? new Vector4(1f, 0.7f, 0.7f, .3f) : new Vector4(0.7f, 1, 0.7f, .3f);
                float scale = building.GetComponent<buildingParameters>().scale;
                building.transform.localScale = new Vector3(scale, scale, scale);
            } else if (newBuilding == Building.Laser) {
                //building.AddComponent<SpriteRenderer>();
                building.GetComponent<SpriteRenderer>().sprite = building.GetComponent<buildingParameters>().sprites[directionToIndex(facing) - 2];
                building.GetComponent<Renderer>().material.color = playerID == Player.PlayerOne ? new Vector4(1f, 0.7f, 0.7f, .3f) : new Vector4(0.7f, 1, 0.7f, .3f);
                float scale = building.GetComponent<buildingParameters>().scale;
                building.transform.localScale = new Vector3(scale, scale, scale);
            } else
                building.GetComponent<Renderer>().material.color = playerID == Player.PlayerOne ? new Vector4(1f, 0.7f, 0.7f, .3f) : new Vector4(0.7f, 1, 0.7f, .3f); // Used for debugging, not necessary with final art
            building.transform.SetParent(buildingContainer.transform);
            building.transform.localPosition = coordsToWorld(x, y);
            if (newBuilding == Building.Laser && building.GetComponent<buildingParameters>().direction == Direction.Down)
            {
                building.transform.localPosition = coordsToWorld(x, y - 0.7f);
            } else if (newBuilding == Building.Laser && building.GetComponent<buildingParameters>().direction == Direction.Up)
            {
                building.transform.localPosition = coordsToWorld(x, y + 0.45f);
            }
            if (newBuilding == Building.Reflecting)
            {
                building.transform.localPosition = coordsToWorld(x, y - 0.135f);
            }
            building.transform.localEulerAngles = new Vector3(90, 0, 0);
            prefabDictionary.Add(new XY(x, y), building);
            // Subtract Cost From Resources
            if (playerID == Player.PlayerOne) {
                resourcesP1 -= getCost(newBuilding, x, playerID);
                buildingNumP1++;
            } else {
                resourcesP2 -= getCost(newBuilding, x, playerID);
                buildingNumP2++;
            }
            // Set base references for getting health later
            if (newBuilding == Building.Base) { if (playerID == Player.PlayerOne) baseP1 = building; else baseP2 = building; }
            // Placement Timer
            if (!instant) {
                // Emit Placement Particle
                emitParticles.genericParticle.emitParticle(x, y, particleType.place);

                GameObject placementTimerObject = MonoBehaviour.Instantiate(placementTimer);
                placementTimerObject.transform.parent = buildingContainer.transform.parent.transform;
                placementTimerObject.transform.localEulerAngles = new Vector3(90f, 0, 0);
                placementTimerObject.transform.localPosition = coordsToWorld(x, y, 1f);
                placementTimerObject.GetComponent<placementTimer>().init(building.GetComponent<buildingParameters>().placementTime, playerID);
            }
            // Specify that the board was updated and that laserLogic needs to run a simulation
            //needsUpdate = true;
            updateSquares();
            
            if (newBuilding != Building.Base && newBuilding != Building.Laser) { 
                Limicator.limicatorObj.changeStones(playerID == Player.PlayerOne ? 0 : 1, State.placing, newBuilding);
                if (playerID == Player.PlayerOne)
                {
                    floatingNumbers.floatingNumbersStruct.checkResource(new XY(x, y), getCost(newBuilding, x, playerID), Player.PlayerOne, State.placing);
                }else
                {
                    floatingNumbers.floatingNumbersStruct.checkResource(new XY(x, y), getCost(newBuilding, x, playerID), Player.PlayerTwo, State.placing);
                }
            }
        } else return false;
        return true;
    }

    // removeBuilding, unlike destroyBuilding, restores half of a building's cost
    public bool removeBuilding (int x, int y, Player playerID)
	{
		if (!validateInput (x, y)) return false;
		if (!grid [y, x].isEmpty && (playerID == grid [y, x].owner || playerID == Player.World) && !grid [y, x].markedForDeath) {
			removalList.Add (new buildingRequest (new XY (x, y), buildingPrefabs [(int)grid [y, x].building].GetComponent<buildingParameters>().removalTime, grid [y, x].building, playerID));
            grid [y, x].markedForDeath = true;

            //Building temp = grid[y, x].building;
            if (grid[y, x].building == Building.Base) { if (grid[y, x].owner == Player.PlayerOne) baseP1 = null; else baseP2 = null; }  // Remove Base Reference
            // Give some resources back to player -> half of placement cost * percentage remaining health
            if (playerID == Player.PlayerOne) {
				resourcesP1 += (getCost (grid [y, x].building, x, playerID, false, true)) * (grid[y, x].health / buildingPrefabs [(int)grid [y, x].building].GetComponent<buildingParameters>().health);
				buildingNumP1--;
			} else {
				resourcesP2 += (getCost (grid [y, x].building, x, playerID, false, true)) * (grid[y, x].health / buildingPrefabs [(int)grid [y, x].building].GetComponent<buildingParameters>().health);
				buildingNumP2--;
			}
            if (resourcesP1 >= resourceLimit) resourcesP1 = resourceLimit;
            if (resourcesP2 >= resourceLimit) resourcesP2 = resourceLimit;
            if (grid[y, x].building != Building.Base && grid[y, x].building != Building.Laser && grid[y, x].building != Building.Redirecting)
            {
                prefabDictionary[new XY(x, y)].GetComponent<Renderer>().material.color = grid[y, x].owner == Player.PlayerOne ? new Vector4(1f, .7f, .7f, .3f) : new Vector4(.7f, 1f, .7f, .3f);
            }
            // Specify that the board was updated and that laserLogic needs to run a simulation
            //needsUpdate = true;
            updateSquares();
            Limicator.limicatorObj.changeStones(grid[y, x].owner == Player.PlayerOne ? 0 : 1, State.removing, Building.Base);
        } else return false;
        return true;
    }

    public bool destroyBuilding(int x, int y)
    {
		SoundManager.PlaySound(inputController.Sounds[1].audioclip, SoundManager.globalSoundsVolume/25, true, .95f, 1.05f);
        if (!validateInput(x, y)) return false;
        if (!grid[y, x].isEmpty) {
            // Emit Destruction Particle
            emitParticles.genericParticle.emitParticle(x, y, particleType.destroy);

            destructionList.Add(new buildingRequest(new XY(x, y), buildingPrefabs[(int)grid[y, x].building].GetComponent<buildingParameters>().removalTime));
			//SoundManager.PlaySound (Sounds[0].audioclip, SoundManager.globalSoundsVolume, true, .5f, 1.5f);
            //Building temp = grid[y, x].building;
			//MonoBehaviour.print("owner: " + grid [y, x].owner);
			if (grid [y, x].owner == Player.PlayerOne) {
				buildingNumP1--;
			} else {
				buildingNumP2--;
			}
            if (grid[y, x].building == Building.Base) { if (grid[y, x].owner == Player.PlayerOne) baseP1 = null; else baseP2 = null; }  // Remove Base Reference
            if (grid[y, x].building == Building.Blocking) {
                if (grid[y, x].owner == Player.PlayerOne) {
                     resourcesP1 += getCost(grid[y, x].building)*laserLogic.laserPowerMultiplier*blockScale;
                } else if (grid[y, x].owner == Player.PlayerTwo) {
                    resourcesP2 += getCost(grid[y, x].building)*laserLogic.laserPowerMultiplier*blockScale;
                }
            }

            // Limicator stuff
            Limicator.limicatorObj.changeStones(grid[y, x].owner == Player.PlayerOne ? 0 : 1, State.removing, Building.Base);

            grid[y, x].isEmpty = true;
            grid[y, x].building = Building.Empty;
            grid[y, x].owner = Player.World;
            grid[y, x].level = 0;
            grid[y, x].health = 0;
            if (grid[y, x].building != Building.Base && grid[y, x].building != Building.Laser && grid[y, x].building != Building.Redirecting)
            {
                prefabDictionary[new XY(x, y)].GetComponent<Renderer>().material.color = grid[y, x].owner == Player.PlayerOne ? new Vector4(1f, .7f, .7f, .3f) : new Vector4(.7f, 1f, .7f, .3f);
            }
            //needsUpdate = true;
            updateSquares();
        } else return false;
        return true;
    }

    public bool moveBuilding(int x, int y, int xNew, int yNew, Player playerID, Direction facing = Direction.Up) // need to add rotation?
    {
        if (!validateInput(x, y) || !validateInput(xNew, yNew)) return false;
        if (!grid[y, x].isEmpty && probeGrid(xNew, yNew, facing, grid[y, x].building, x, y) && (grid[yNew, xNew].isEmpty || (x == xNew && y == yNew)) && playerID == grid[y, x].owner) {
            // Subtract some resources for move
            GridItem temp = grid[y, x];
            if (playerID == Player.PlayerOne) resourcesP1 -= getCost(grid[y, x].building, xNew, playerID, true);
            else resourcesP2 -= getCost(grid[y, x].building, xNew, playerID, true);
            // Move Building
            GameObject building = prefabDictionary[new XY(x, y)];
            if (x != xNew || y != yNew)
            { // If moving to same place, skip this and just rotate instead
                grid[yNew, xNew].isEmpty = false;
                grid[yNew, xNew].building = grid[y, x].building;
                grid[yNew, xNew].owner = playerID;
                grid[yNew, xNew].level = grid[y, x].level;
                grid[yNew, xNew].health = grid[y, x].health;
                grid[y, x].isEmpty = true;
                grid[y, x].building = Building.Empty;
                grid[y, x].owner = Player.World;
                grid[y, x].level = 0;
                grid[y, x].health = 0;
                // Move Building Prefab
                building.GetComponent<buildingParameters>().x = xNew;
                building.GetComponent<buildingParameters>().y = yNew;
                building.transform.localPosition = new Vector3((-dimX / 2) + xNew + 0.5f, 0, (-dimY / 2) + yNew + 0.5f);
                prefabDictionary.Remove(new XY(x, y));
                prefabDictionary.Add(new XY(xNew, yNew), building);
            }
            grid[yNew, xNew].direction = facing;
            // Rotate
            if(canRotate(grid[yNew, xNew].building)) building.GetComponent<SpriteRenderer>().sprite = building.GetComponent<buildingParameters>().sprites[directionToIndex(facing)];
            // Specify that the board was updated and that laserLogic needs to run a simulation
            needsUpdate = true;
            updateSquares();
        } else return false;
        return true;
    }

    /*public bool swapBuilding(int x, int y, int xNew, int yNew, Player playerID) // Need to add rotation? // Never going to be used?
    {
        if (!validateInput(x, y) || !validateInput(xNew, yNew)) return false;
        if (!grid[y, x].isEmpty && !grid[yNew, xNew].isEmpty && playerID == grid[y, x].owner && playerID == grid[yNew, xNew].owner) {
            Building tempBuild = grid[yNew, xNew].building;
            Direction tempDir = grid[yNew, xNew].direction;
            int[] tempWeakSides = grid[yNew, xNew].weakSides;
            byte tempLevel = grid[yNew, xNew].level;
            float tempHealth = grid[yNew, xNew].health;
            grid[yNew, xNew].building = grid[y, x].building;
            grid[yNew, xNew].direction = grid[y, x].direction;
            grid[yNew, xNew].weakSides = grid[y, x].weakSides;
            grid[yNew, xNew].level = grid[y, x].level;
            grid[yNew, xNew].health = grid[y, x].health;
            grid[y, x].building = tempBuild;
            grid[y, x].direction = tempDir;
            grid[y, x].weakSides = tempWeakSides;
            grid[y, x].level = tempLevel;
            grid[y, x].health = tempHealth;
            // Swap Building Prefab
            GameObject building = prefabDictionary[new XY(x, y)];
            GameObject building2 = prefabDictionary[new XY(xNew, yNew)];
            building.GetComponent<buildingParameters>().x = xNew;
            building.GetComponent<buildingParameters>().y = yNew;
            building2.GetComponent<buildingParameters>().x = x;
            building2.GetComponent<buildingParameters>().y = y;
            building.transform.localPosition = new Vector3((-dimX / 2) + xNew + 0.5f, 0, (-dimY / 2) + yNew + 0.5f);
            building.transform.localPosition = new Vector3((-dimX / 2) + x + 0.5f, 0, (-dimY / 2) + y + 0.5f);
            prefabDictionary.Remove(new XY(x, y));
            prefabDictionary.Remove(new XY(xNew, yNew));
            prefabDictionary.Add(new XY(xNew, yNew), building);
            prefabDictionary.Add(new XY(x, y), building2);
            // Subtract some resources for swapped buildings (equal to the cost to individually move each building / 2)
            if (playerID == Player.PlayerOne) resourcesP1 -= getCost(building.GetComponent<buildingParameters>().building, xNew, playerID, false, false, true);
            else resourcesP2 -= getCost(building.GetComponent<buildingParameters>().building, xNew, playerID, false, false, true);
            if (playerID == Player.PlayerOne) resourcesP1 -= getCost(building2.GetComponent<buildingParameters>().building, x, playerID, false, false, true);
            else resourcesP2 -= getCost(building2.GetComponent<buildingParameters>().building, x, playerID, false, false, true);
            // Specify that the board was updated and that laserLogic needs to run a simulation
            needsUpdate = true;
        } else return false;
        return true;
    }*/

    /* Backlog
    private bool canBeUpgraded(GridItem item)
    {
        // Add upgrade conditions here
        return true;
    }

    public bool upgradeBuilding(int x, int y, Player playerID)
    {
        if (!validateInput(x, y)) return false;
        if (!grid[y, x].isEmpty && playerID == grid[y, x].owner && canBeUpgraded(grid[y, x])) {
            grid[y, x].level++;
        } else return false;
        return true;
    }*/
}

public class gridManager : MonoBehaviour
{
    // Use this to make HP appear artificially higher on UI etc. (ie. gridManager.hpScale)
    // EX: Lets say an actual damage value is 1.337f. With a hpScale of 100f it would show up on screen as 133.7
    public const float hpScale = 10f; 

    public static Grid theGrid;
    public int boardWidth = 14;
    public int boardHeight = 10;
    public float startingResources = 20;
    public int startingBuildingNum = 0;
    public GameObject Base;
    public GameObject Base2;
    public GameObject Laser;
    public GameObject Laser2;
    public GameObject Block;
    public GameObject Block2;
    public GameObject Reflect;
    public GameObject Reflect2;
    public GameObject Refract;
    public GameObject Refract2;
    public GameObject Redirect;
    public GameObject Redirect2;
    public GameObject Resource;
    public GameObject Resource2;
    public GameObject Portal;
    public GameObject Portal2;
    public float limit;

    public GameObject tutorialObject;
    public GameObject empty;
    public GameObject dot;
    public GameObject placementTimerObj;
    public int blockResourceScale;
    private GameObject buildingContainer;
    private int deletionCount = 0;

    public void initGrid()
    {
        theGrid = new Grid(boardWidth, boardHeight, buildingContainer, Base, Base2, Laser, Laser2, Block, Block2, Reflect, Reflect2, Refract, Refract2, Redirect, Redirect2, Resource, Resource2, Portal, Portal2, startingResources,
            startingBuildingNum, empty, dot, placementTimerObj, tutorialObject, limit, blockResourceScale);
    }

    void Awake()
    {
        buildingContainer = new GameObject("buildingContainer");
        buildingContainer.transform.SetParent(gameObject.transform);
        initGrid();
    }
    
    void LateUpdate()
    {
        deletionCount = 0;
        // Place buildings
        for (int i = 0; i < theGrid.placementList.Count; i++) {
            if (theGrid.placementList[i].updateTime(Time.deltaTime) <= 0f) {
                int x = theGrid.placementList[i].coords.x;
                int y = theGrid.placementList[i].coords.y;
                theGrid.grid[y, x].isEmpty = false;
                theGrid.grid[y, x].building = theGrid.placementList[i].building;
                theGrid.grid[y, x].owner = theGrid.placementList[i].owner;
                theGrid.grid[y, x].direction = theGrid.placementList[i].direction;
                theGrid.grid[y, x].health = theGrid.placementList[i].health;
                theGrid.prefabDictionary[new XY(x,y)].GetComponent<Renderer>().material.color = theGrid.grid[y, x].owner == Player.PlayerOne ? new Vector4(1f, .7f, .7f, 1f) : new Vector4(.7f, 1f, .7f, 1f);
                
                theGrid.queueUpdate();
                deletionCount++;

                ghostLaser.ghostUpdateNeeded = true;
                theGrid.updateSquares();
            }
        }
        for (int i = 0; i < deletionCount; i++) {
			theGrid.placementList.RemoveAt(0);
        }
        deletionCount = 0;
        // Remove buildings
        for (int i = 0; i < theGrid.removalList.Count; i++) {
            if (theGrid.removalList[i].updateTime(Time.deltaTime) <= 0f) {
                int x = theGrid.removalList[i].coords.x;
                int y = theGrid.removalList[i].coords.y;
                theGrid.grid[y, x].isEmpty = true;
                theGrid.grid[y, x].building = Building.Empty;
                theGrid.grid[y, x].owner = Player.World;
                theGrid.grid[y, x].level = 0;
                theGrid.grid[y, x].health = 0;
                theGrid.grid[y, x].markedForDeath = false;
                if (theGrid.prefabDictionary.ContainsKey(new XY(x, y))) {
                    DestroyImmediate(theGrid.prefabDictionary[new XY(x, y)]);
                    theGrid.prefabDictionary.Remove(new XY(x, y));
                }
                theGrid.queueUpdate();
                deletionCount++;

                ghostLaser.ghostUpdateNeeded = true;
                theGrid.updateSquares();
            }
        }
        for (int i = 0; i < deletionCount; i++) {
            theGrid.removalList.RemoveAt(0);
        }
        deletionCount = 0;
        // Destroy buildings
        for (int i = 0; i < theGrid.destructionList.Count; i++) {
            if (theGrid.destructionList[i].updateTime(Time.deltaTime) <= 0f) {
                int x = theGrid.destructionList[i].coords.x;
                int y = theGrid.destructionList[i].coords.y;
                /*theGrid.grid[y, x].isEmpty = true;
                theGrid.grid[y, x].building = Building.Empty;
                theGrid.grid[y, x].owner = Player.World;
                theGrid.grid[y, x].level = 0;
                theGrid.grid[y, x].health = 0;
                theGrid.grid[y, x].markedForDeath = false;*/
                if (theGrid.prefabDictionary.ContainsKey(new XY(x, y))) {
                    DestroyImmediate(theGrid.prefabDictionary[new XY(x, y)]);
                    theGrid.prefabDictionary.Remove(new XY(x, y));
                }
                theGrid.queueUpdate();
                deletionCount++;

                ghostLaser.ghostUpdateNeeded = true;
                theGrid.updateSquares();
            }
        }
        for (int i = 0; i < deletionCount; i++) {
			theGrid.destructionList.RemoveAt(0);
        }
    }


    // Debug building placements
    // Comment out to hide gizmos
    /*void OnDrawGizmos()
    {
        // Note: one grid cell = 1x1 meter in unity
        int dimX = theGrid.getDimX();
        int dimY = theGrid.getDimY();

        for (int row = 0; row < theGrid.getDimY(); row++) {
            for (int col = 0; col < theGrid.getDimX(); col++) {
                Gizmos.color = new Color(1f, 1f, 1f, .8f);
                //Gizmos.DrawCube(new Vector3((-dimX/2)+col+0.5f, -0.5f, (-dimY/2)+row+0.5f), new Vector3(.8f, 1f, .8f));
                if (theGrid.getCellInfo(col, row).isEmpty) {
                    Gizmos.color = new Color(1f, 1f, 1f, .8f);
                    //Gizmos.DrawCube(new Vector3((-dimX / 2) + col + 0.5f, 0.5f, (-dimY / 2) + row + 0.5f), Vector3.one);
                }
            }
        }
    }*/

}
