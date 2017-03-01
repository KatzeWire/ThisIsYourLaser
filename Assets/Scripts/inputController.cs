﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum State { placeBase, placeLaser, placingLaser, placing, moving, placingMove, idle };

public class inputController : MonoBehaviour {

    // These should be gameObjects that contain a sprite renderer
    public GameObject cursorObjP1;
    public GameObject cursorObjP2;

    public GameObject indicatorP1;
    public GameObject indicatorP2;

    // Sprites for cursor appearance
    public Sprite P1BaseSprite;
    public Sprite P1BlockSprite;
    public Sprite P1LaserSprite;
    public Sprite P1ReflectSprite;
    public Sprite P1RefractSprite;
    public Sprite P1RedirectSprite;
    public Sprite P1ResourceSprite;

    public Sprite P2BaseSprite;
    public Sprite P2BlockSprite;
    public Sprite P2LaserSprite;
    public Sprite P2ReflectSprite;
    public Sprite P2RefractSprite;
    public Sprite P2RedirectSprite;
    public Sprite P2ResourceSprite;

    // Cursor movement speed
    public float cursorSpeed = 10f;

    // Pause menu
    public GameObject PauseMenu;

    public struct Cursor
    {
        public int x, y;
        public XY moveOrigin;
        public Building moveBuilding;
        public Building selection;
        public Direction direction;
        public State state;
        public Cursor(int X, int Y, Direction dir, Building selected, State current)
        {
            x = X;
            y = Y;
            direction = dir;
            selection = selected;
            state = current;
            moveOrigin = new XY(-1, -1);
            moveBuilding = Building.Empty;
        }
    }

    private int clamp(int value, int min, int max) {
        if (value > max) return max;
        else if (value < min) return min;
        return value;
    }

    public static Cursor cursorP1, cursorP2;
    private int xEnd, yEnd;
    private int cycleP1, cycleP2;
    private bool p1HasPlacedBase = false, p2HasPlacedBase = false;
    private Queue<XY> moveQueueP1 = new Queue<XY>();
    private Queue<XY> moveQueueP2 = new Queue<XY>();

    void Start () {
        cycleP1 = 0;
        cycleP2 = 0;
        xEnd = gridManager.theGrid.getDimX() - 1;
        yEnd = gridManager.theGrid.getDimY() - 1;
        cursorP1 = new Cursor(0, 0, Direction.Right, Building.Blocking, State.placeBase);
        cursorP2 = new Cursor(xEnd, yEnd, Direction.Left, Building.Blocking, State.placeBase);
        PauseMenu = GameObject.Find("Pause Menu");
        // Set initial cursor positions
        cursorObjP1.transform.position = new Vector3(cursorP1.x + (-gridManager.theGrid.getDimX() / 2f + 0.5f), 0.01f, cursorP1.y + (-gridManager.theGrid.getDimY() / 2f + 0.5f));
        cursorObjP2.transform.position = new Vector3(cursorP2.x + (-gridManager.theGrid.getDimX() / 2f + 0.5f), 0.01f, cursorP2.y + (-gridManager.theGrid.getDimY() / 2f + 0.5f));
    }

    void Update()
    {
        // Check that the game isn't paused
        if (PauseMenu.activeInHierarchy == false)
        {
            // Cursor Selection P1
            if (Input.GetKeyDown("1")) cursorP1.selection = Building.Blocking;
            else if (Input.GetKeyDown("2")) cursorP1.selection = Building.Reflecting;
            else if (Input.GetKeyDown("3")) cursorP1.selection = Building.Refracting;
            else if (Input.GetKeyDown("4")) cursorP1.selection = Building.Redirecting;
            else if (Input.GetKeyDown("5")) cursorP1.selection = Building.Resource;
            // Cycle P1
            if (Input.GetButtonDown("cycleR_1"))
            {
                if (cursorP1.selection == Building.Redirecting) cursorP1.selection = Building.Blocking;
                else cursorP1.selection += 1;
            }
            else if (Input.GetButtonDown("cycleL_1"))
            {
                if (cursorP1.selection == Building.Blocking) cursorP1.selection = Building.Redirecting;
                else cursorP1.selection -= 1;
            }

            // Cursor Selection P2
            if (Input.GetKeyDown("7")) cursorP2.selection = Building.Blocking;
            else if (Input.GetKeyDown("8")) cursorP2.selection = Building.Reflecting;
            else if (Input.GetKeyDown("9")) cursorP2.selection = Building.Refracting;
            else if (Input.GetKeyDown("0")) cursorP2.selection = Building.Redirecting;
            else if (Input.GetKeyDown("-")) cursorP2.selection = Building.Resource;
            // Cycle P2
            if (Input.GetButtonDown("cycleR_2"))
            {
                if (cursorP2.selection == Building.Redirecting) cursorP2.selection = Building.Blocking;
                else cursorP2.selection += 1;
            }
            else if (Input.GetButtonDown("cycleL_2"))
            {
                if (cursorP2.selection == Building.Blocking) cursorP2.selection = Building.Redirecting;
                else cursorP2.selection -= 1;
            }

            if (cursorP1.state != State.placing && cursorP1.state != State.placingLaser && cursorP1.state != State.placingMove)
            {
                // Cursor Movement P1
                if (Input.GetButtonDown("up_1") || Input.GetAxis("xboxLeftVert") == 1) { cursorP1.y = clamp(cursorP1.y + 1, 0, yEnd); moveQueueP1.Enqueue(new XY(cursorP1.x, cursorP1.y)); }
                else if (Input.GetButtonDown("down_1") || Input.GetAxis("xboxLeftVert") == -1) { cursorP1.y = clamp(cursorP1.y - 1, 0, yEnd); moveQueueP1.Enqueue(new XY(cursorP1.x, cursorP1.y)); }
                if ((Input.GetButtonDown("right_1") || Input.GetAxis("xboxLeftHor") == 1) && (cursorP1.state != State.placeLaser && cursorP1.state != State.placeBase)) { cursorP1.x = clamp(cursorP1.x + 1, 0, xEnd); moveQueueP1.Enqueue(new XY(cursorP1.x, cursorP1.y)); }
                else if (Input.GetButtonDown("left_1") || Input.GetAxis("xboxLeftHor") == -1) { cursorP1.x = clamp(cursorP1.x - 1, 0, xEnd); moveQueueP1.Enqueue(new XY(cursorP1.x, cursorP1.y)); }
            }
            else
            {
                // Cursor Rotation P1
                bool selectionMade = false;
                if (Input.GetButtonDown("up_1") || Input.GetAxis("xboxLeftVert") == 1) { cursorP1.direction = Direction.Up; selectionMade = true; }
                else if (Input.GetButtonDown("down_1") || Input.GetAxis("xboxLeftVert") == -1) { cursorP1.direction = Direction.Down; selectionMade = true; }
                else if (Input.GetButtonDown("right_1") || Input.GetAxis("xboxLeftHor") == 1) { cursorP1.direction = Direction.Right; selectionMade = true; }
                else if ((Input.GetButtonDown("left_1") || Input.GetAxis("xboxLeftHor") == -1)) { cursorP1.direction = Direction.Left; selectionMade = true; }
                if (selectionMade)
                { // If placing or moving, finalize action
                    if (cursorP1.state == State.placingMove) move(Player.PlayerOne, cursorP1.state);
                    else place(Player.PlayerOne, cursorP1.state);
                }
            }

            if (cursorP2.state != State.placing && cursorP2.state != State.placingLaser && cursorP2.state != State.placingMove)
            {
                // Cursor Movement P2
                if (Input.GetButtonDown("up_2") || Input.GetAxis("xboxLeftVert2") == 1) { cursorP2.y = clamp(cursorP2.y + 1, 0, yEnd); moveQueueP2.Enqueue(new XY(cursorP2.x, cursorP2.y)); }
                else if (Input.GetButtonDown("down_2") || Input.GetAxis("xboxLeftVert2") == -1) { cursorP2.y = clamp(cursorP2.y - 1, 0, yEnd); moveQueueP2.Enqueue(new XY(cursorP2.x, cursorP2.y)); }
                if (Input.GetButtonDown("right_2") || Input.GetAxis("xboxLeftHor2") == 1) { cursorP2.x = clamp(cursorP2.x + 1, 0, xEnd); moveQueueP2.Enqueue(new XY(cursorP2.x, cursorP2.y)); }
                else if ((Input.GetButtonDown("left_2") || Input.GetAxis("xboxLeftHor2") == -1) && (cursorP2.state != State.placeLaser && cursorP2.state != State.placeBase)) { cursorP2.x = clamp(cursorP2.x - 1, 0, xEnd); moveQueueP2.Enqueue(new XY(cursorP2.x, cursorP2.y)); }
            }
            else
            {
                // Cursor Rotation P1
                bool selectionMade = false;
                if (Input.GetButtonDown("up_2") || Input.GetAxis("xboxLeftVert2") == 1) { cursorP2.direction = Direction.Up; selectionMade = true; }
                else if (Input.GetButtonDown("down_2") || Input.GetAxis("xboxLeftVert2") == -1) { cursorP2.direction = Direction.Down; selectionMade = true; }
                else if (Input.GetButtonDown("right_2") || Input.GetAxis("xboxLeftHor2") == 1) { cursorP2.direction = Direction.Right; selectionMade = true; }
                else if (Input.GetButtonDown("left_2") || Input.GetAxis("xboxLeftHor2") == -1) { cursorP2.direction = Direction.Left; selectionMade = true; }
                if (selectionMade)
                { // If placing or moving, finalize action
                    if (cursorP2.state == State.placingMove) move(Player.PlayerTwo, cursorP2.state);
                    else place(Player.PlayerTwo, cursorP2.state);
                }
            }

            // Cursor Functions P1
            if (Input.GetButtonDown("place_1")) place(Player.PlayerOne, cursorP1.state);
            else if (Input.GetButtonDown("move_1")) move(Player.PlayerOne, cursorP1.state);
            else if (Input.GetButtonDown("remove_1")) remove(Player.PlayerOne, cursorP1.state);

            // Cursor Functions P2
            if (Input.GetButtonDown("place_2")) place(Player.PlayerTwo, cursorP2.state);
            else if (Input.GetButtonDown("move_2")) move(Player.PlayerTwo, cursorP2.state);
            else if (Input.GetButtonDown("remove_2")) remove(Player.PlayerTwo, cursorP2.state);

            // Update Cursor Appearance P1
            if (cursorP1.state == State.placeBase) cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1BaseSprite;
            else if (cursorP1.state == State.placeLaser || cursorP1.state == State.placingLaser) cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1LaserSprite;
            else
            {
                switch (cursorP1.selection)
                {
                    case Building.Blocking: cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1BlockSprite; break;
                    case Building.Reflecting: cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1ReflectSprite; break;
                    case Building.Refracting: cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1RefractSprite; break;
                    case Building.Redirecting: cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1RedirectSprite; break;
                    case Building.Resource: cursorObjP1.GetComponent<SpriteRenderer>().sprite = P1ResourceSprite; break;
                }
            }

            // Update Cursor Appearance P2
            if (cursorP2.state == State.placeBase) cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2BaseSprite;
            else if (cursorP2.state == State.placeLaser || cursorP2.state == State.placingLaser) cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2LaserSprite;
            else
            {
                switch (cursorP2.selection)
                {
                    case Building.Blocking: cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2BlockSprite; break;
                    case Building.Reflecting: cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2ReflectSprite; break;
                    case Building.Refracting: cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2RefractSprite; break;
                    case Building.Redirecting: cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2RedirectSprite; break;
                    case Building.Resource: cursorObjP2.GetComponent<SpriteRenderer>().sprite = P2ResourceSprite; break;
                }
            }

            float xOff = -gridManager.theGrid.getDimX() / 2f + 0.5f;
            float yOff = -gridManager.theGrid.getDimY() / 2f + 0.5f;

            // Update Cursor Position P1
            if (moveQueueP1.Count > 0)
            {
                cursorObjP1.transform.position = Vector3.MoveTowards(cursorObjP1.transform.position, new Vector3(moveQueueP1.Peek().x + xOff, 0.01f, moveQueueP1.Peek().y + yOff), Time.deltaTime * cursorSpeed * (0.8f + Mathf.Pow(moveQueueP1.Count, 2) * 0.2f));
                if (Vector2.Distance(new Vector2(cursorObjP1.transform.position.x, cursorObjP1.transform.position.z), new Vector2(moveQueueP1.Peek().x + xOff, moveQueueP1.Peek().y + yOff)) == 0f) moveQueueP1.Dequeue();
            }
            // Update Cursor Position P2
            if (moveQueueP2.Count > 0)
            {
                cursorObjP2.transform.position = Vector3.MoveTowards(cursorObjP2.transform.position, new Vector3(moveQueueP2.Peek().x + xOff, 0.01f, moveQueueP2.Peek().y + yOff), Time.deltaTime * cursorSpeed * (0.8f + Mathf.Pow(moveQueueP2.Count, 2) * 0.2f));
                if (Vector2.Distance(new Vector2(cursorObjP2.transform.position.x, cursorObjP2.transform.position.z), new Vector2(moveQueueP2.Peek().x + xOff, moveQueueP2.Peek().y + yOff)) == 0f) moveQueueP2.Dequeue();
            }

            // Update Cursor Indicator
            if (cursorP1.state == State.placing)
            {
                indicatorP1.GetComponent<SpriteRenderer>().enabled = true;
            }
            else indicatorP1.GetComponent<SpriteRenderer>().enabled = false;
            if (cursorP2.state == State.placing)
            {
                indicatorP2.GetComponent<SpriteRenderer>().enabled = true;
            }
            else indicatorP2.GetComponent<SpriteRenderer>().enabled = false;
        }
    }

    private Building cycleToBuilding(int index)
    {
        switch(index) {
            case 0: return Building.Blocking;
            case 1: return Building.Reflecting;
            case 2: return Building.Refracting;
            case 3: return Building.Redirecting;
        }
        return Building.Resource;
    }

    private void place(Player player, State currentState)
    {
        if (currentState == State.placeBase) {
            if (player == Player.PlayerOne) {
                if (cursorP1.x > 0) print("Base must be placed on the edge of the board");
                else {
                    gridManager.theGrid.placeBuilding(0, cursorP1.y, Building.Base, Player.PlayerOne);
                    cursorP1.state = State.placeLaser; p1HasPlacedBase = true;
                }
            } else {
                if (cursorP2.x < xEnd) print("Base must be placed on the edge of the board");
                else {
                    gridManager.theGrid.placeBuilding(xEnd, cursorP2.y, Building.Base, Player.PlayerTwo);
                    cursorP2.state = State.placeLaser; p2HasPlacedBase = true;
                }
            }
        } else if (currentState == State.placeLaser && p1HasPlacedBase && p2HasPlacedBase) {
            if (player == Player.PlayerOne) {
                if (cursorP1.x > 0) print("Laser must be placed on the edge of the board");
                else {
                    if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) != Building.Empty) print("Laser can not be placed on top of base.");
                    else cursorP1.state = State.placingLaser;
                }
            } else {
                if (cursorP2.x < xEnd) print("Laser must be placed on the edge of the board");
                else {
                    if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) != Building.Empty) print("Laser can not be placed on top of base.");
                    else cursorP2.state = State.placingLaser;
                }
            }
        } else if (currentState == State.placingLaser) {
            if (player == Player.PlayerOne) {
                if (cursorP1.direction == Direction.Up) { if (gridManager.theGrid.placeBuilding(0, cursorP1.y, Building.Laser, Player.PlayerOne, Direction.Up)) { laserLogic.laserHeadingP1 = Direction.NE; cursorP1.state = State.idle; } else { cursorP1.state = State.placeLaser; } }
                else if (cursorP1.direction == Direction.Down) { if (gridManager.theGrid.placeBuilding(0, cursorP1.y, Building.Laser, Player.PlayerOne, Direction.Down)) { laserLogic.laserHeadingP1 = Direction.SE; cursorP1.state = State.idle; } else { cursorP1.state = State.placeLaser; } }
                else print("Press the up or down direction keys to place laser");
            } else {
                if (cursorP2.direction == Direction.Up) { if (gridManager.theGrid.placeBuilding(xEnd, cursorP2.y, Building.Laser, Player.PlayerTwo, Direction.Up)) { laserLogic.laserHeadingP2 = Direction.NW; cursorP2.state = State.idle; } else { cursorP2.state = State.placeLaser; } }
                else if (cursorP2.direction == Direction.Down) { if (gridManager.theGrid.placeBuilding(xEnd, cursorP2.y, Building.Laser, Player.PlayerTwo, Direction.Down)) { laserLogic.laserHeadingP2 = Direction.SW; cursorP2.state = State.idle; } else { cursorP2.state = State.placeLaser; } }
                else print("Press the up or down direction keys to place laser");
            }
        } else if (currentState == State.placing) {
            if (player == Player.PlayerOne) {
                if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) != Building.Empty) print("You can not place here, selection is no longer empty");
                else { if (!gridManager.theGrid.placeBuilding(cursorP1.x, cursorP1.y, cursorP1.selection, Player.PlayerOne, cursorP1.direction)) print("Placing failed."); cursorP1.state = State.idle; }
            } else {
                if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) != Building.Empty) print("You can not place here, selection is no longer empty");
                else { if (!gridManager.theGrid.placeBuilding(cursorP2.x, cursorP2.y, cursorP2.selection, Player.PlayerTwo, cursorP2.direction)) print("Placing failed."); cursorP2.state = State.idle; }
            }
        } else if (currentState == State.idle) {
            if (player == Player.PlayerOne) {
                if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) != Building.Empty) print("You can not place here, selection is not empty");
                else if (gridManager.theGrid.getCost(cursorP1.selection, cursorP1.x, Player.PlayerOne) < gridManager.theGrid.getResourcesP1()) cursorP1.state = State.placing;
                else print("Not enough resources to place.");
            } else {
                if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) != Building.Empty) print("You can not place here, selection is not empty");
                else if (gridManager.theGrid.getCost(cursorP2.selection, cursorP2.x, Player.PlayerTwo) < gridManager.theGrid.getResourcesP2()) cursorP2.state = State.placing;
                else print("Not enough resources to place.");
            }
        } else {
            print("Can not place, busy with some other action.");
        }

    }

    private void move(Player player, State currentState)
    {
        if (currentState == State.moving) {
            if (player == Player.PlayerOne) {
                if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) != Building.Empty && !new XY(cursorP1.x, cursorP1.y).Equals(cursorP1.moveOrigin)) print("You can not move to here, selection is not empty");
                else if (gridManager.theGrid.getCost(cursorP1.moveBuilding, cursorP1.x, Player.PlayerOne, true) < gridManager.theGrid.getResourcesP1()) cursorP1.state = State.placingMove;
                else print("Not enough resources to move.");
            } else {
                if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) != Building.Empty && !new XY(cursorP2.x, cursorP2.y).Equals(cursorP2.moveOrigin)) print("You can not move to here, selection is not empty");
                else if (gridManager.theGrid.getCost(cursorP2.moveBuilding, cursorP2.x, Player.PlayerTwo, true) < gridManager.theGrid.getResourcesP2()) cursorP2.state = State.placingMove;
                else print("Not enough resources to move.");
            }
        } else if (currentState == State.placingMove) {
            if (player == Player.PlayerOne) {
                if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) != Building.Empty && !cursorP1.moveOrigin.Equals(new XY(cursorP1.x, cursorP1.y))) print("You can not move here, selection is no longer empty");
                else { if (!gridManager.theGrid.moveBuilding(cursorP1.moveOrigin.x, cursorP1.moveOrigin.y, cursorP1.x, cursorP1.y, Player.PlayerOne, cursorP1.direction)) print("Moving failed."); cursorP1.state = State.idle; }
            } else {
                if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) != Building.Empty && !cursorP2.moveOrigin.Equals(new XY(cursorP2.x, cursorP2.y))) print("You can not move here, selection is no longer empty");
                else { if (!gridManager.theGrid.moveBuilding(cursorP2.moveOrigin.x, cursorP2.moveOrigin.y, cursorP2.x, cursorP2.y, Player.PlayerTwo, cursorP2.direction)) print("Moving failed."); cursorP2.state = State.idle; }
            }
        } else if (currentState == State.idle) {
            if (player == Player.PlayerOne) {
                if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) == Building.Empty || gridManager.theGrid.getCellInfo(cursorP1.x, cursorP1.y).owner != Player.PlayerOne) print("Invalid move target.");
                else if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) == Building.Base || gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) == Building.Laser) print("Cannot move this building.");
                else { cursorP1.moveOrigin = new XY(cursorP1.x, cursorP1.y); cursorP1.moveBuilding = gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y); cursorP1.state = State.moving; }
            } else {
                if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) == Building.Empty || gridManager.theGrid.getCellInfo(cursorP2.x, cursorP2.y).owner != Player.PlayerTwo) print("Invalid move target.");
                else if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) == Building.Base || gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) == Building.Laser) print("Cannot move this building.");
                else { cursorP2.moveOrigin = new XY(cursorP2.x, cursorP2.y); cursorP2.moveBuilding = gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y); cursorP2.state = State.moving; }
            }
        } else {
            print("Can not move, busy with some other action.");
        }
    }

    private void remove(Player player, State currentState)
    {
        if (currentState == State.idle) {
            if (player == Player.PlayerOne) {
                if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) == Building.Empty) print("Nothing to remove here.");
                else if (gridManager.theGrid.getCellInfo(cursorP1.x, cursorP1.y).owner != Player.PlayerOne) print("You can not remove a building that you do not own.");
                else if (gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) == Building.Base || gridManager.theGrid.getBuilding(cursorP1.x, cursorP1.y) == Building.Laser) print("Cannot remove this building.");
                else { if (!gridManager.theGrid.removeBuilding(cursorP1.x, cursorP1.y, Player.PlayerOne)) print("Removing failed."); }
            } else {
                if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) == Building.Empty) print("Nothing to remove here.");
                else if (gridManager.theGrid.getCellInfo(cursorP2.x, cursorP2.y).owner != Player.PlayerTwo) print("You can not remove a building that you do not own.");
                else if (gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) == Building.Base || gridManager.theGrid.getBuilding(cursorP2.x, cursorP2.y) == Building.Laser) print("Cannot remove this building.");
                else { if (!gridManager.theGrid.removeBuilding(cursorP2.x, cursorP2.y, Player.PlayerTwo)) print("Removing failed."); }
            }
        } else {
            print("Can not remove, busy with some other action.");
        }
    }
}
