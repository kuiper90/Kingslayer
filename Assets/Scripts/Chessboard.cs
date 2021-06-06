using System;
using System.Collections.Generic;
using UnityEngine;

public class Chessboard : MonoBehaviour
{
    [Header("Art work")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //Logic
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private const int TILE_COUNT_X = 30;
    private const int TILE_COUNT_Y = 30;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;

    private void Awake()
    {
        isWhiteTurn = true;

        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();
    }

    private void Update()
    {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 15000, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            //Get the indexes of the tile that was hit
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            //If hovering a tile after not hovering any tile
            if (currentHover == -Vector2Int.one) // on default
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //If already hovering a tile, change the previous one
            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ?
                                                               LayerMask.NameToLayer("Highlight") :
                                                               LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //If mouse is pressed down
            if (Input.GetMouseButtonDown(0))
            {
                if (chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    //Is it our turn?
                    if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn) ||
                        (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        //Get a list of where I can go, highlight tiles as well
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                        HighlightTiles();
                    }
                }
            }

            //If releasing the mouse button
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))   
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool validMove = MoveTo(currentlyDragging, hitPosition.x, hitPosition.y);
                if (!validMove)
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                }
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? 
                                                               LayerMask.NameToLayer("Highlight") : 
                                                               LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if (currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null; 
                RemoveHighlightTiles();
            }
        }

        //If dragging a piece
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
            {
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
            }
        }
    }

    //Generate the chess board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        // yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, yOffset, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
            }
        }
    }

    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject($"X:{x}, Y:{y}");
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] triangle = new int[] { 0, 1, 2, 1, 3, 2};

        mesh.vertices = vertices;
        mesh.triangles = triangle;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    //Spaning of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
        int whiteTeam = 0;
        int blackTeam = 1;

        int x = 10;
        int y = 5;
        ConvertAndPutPiece(x, y - 2, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x, y - 1, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x, y, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 1, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 2, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 3, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 4, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 2, y + 5, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 2, y + 6, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 2, y + 7, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 8, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 9, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 10, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x + 1, y + 11, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x, y + 12, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x, y + 13, ChessPieceType.Pawn, blackTeam);
        ConvertAndPutPiece(x, y + 14, ChessPieceType.Pawn, blackTeam);

        ConvertAndPutPiece(x, y + 1, ChessPieceType.Bishop, blackTeam);
        ConvertAndPutPiece(x - 1, y + 2, ChessPieceType.Rook, blackTeam);
        ConvertAndPutPiece(x, y + 3, ChessPieceType.Rook, blackTeam);
        ConvertAndPutPiece(x - 1, y + 4, ChessPieceType.Bishop, blackTeam);
        ConvertAndPutPiece(x + 1, y + 5, ChessPieceType.Bishop, blackTeam);
        ConvertAndPutPiece(x, y + 6, ChessPieceType.Bishop, blackTeam);
        ConvertAndPutPiece(x + 1, y + 6, ChessPieceType.Rook, blackTeam);
        ConvertAndPutPiece(x + 1, y + 7, ChessPieceType.Bishop, blackTeam);
        ConvertAndPutPiece(x - 1, y + 8, ChessPieceType.Bishop, blackTeam);
        ConvertAndPutPiece(x, y + 9, ChessPieceType.Rook, blackTeam);
        ConvertAndPutPiece(x - 1, y + 10, ChessPieceType.Rook, blackTeam);
        ConvertAndPutPiece(x, y + 11, ChessPieceType.Bishop, blackTeam);

        x = 20;

        ConvertAndPutPiece(x, y - 2, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x, y - 1, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x, y, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 1, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 2, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 3, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 4, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 2, y + 5, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 2, y + 6, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 2, y + 7, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 8, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 9, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 10, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 11, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x, y + 12, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x, y + 13, ChessPieceType.Pawn, whiteTeam);
        ConvertAndPutPiece(x, y + 14, ChessPieceType.Pawn, whiteTeam);

        ConvertAndPutPiece(x, y + 1, ChessPieceType.Bishop, whiteTeam);
        ConvertAndPutPiece(x + 1, y + 2, ChessPieceType.Rook, whiteTeam);
        ConvertAndPutPiece(x, y + 3, ChessPieceType.Rook, whiteTeam);
        ConvertAndPutPiece(x + 1, y + 4, ChessPieceType.Bishop, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 5, ChessPieceType.Bishop, whiteTeam);
        ConvertAndPutPiece(x, y + 6, ChessPieceType.Bishop, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 6, ChessPieceType.Rook, whiteTeam);
        ConvertAndPutPiece(x - 1, y + 7, ChessPieceType.Bishop, whiteTeam);
        ConvertAndPutPiece(x + 1, y + 8, ChessPieceType.Bishop, whiteTeam);
        ConvertAndPutPiece(x, y + 9, ChessPieceType.Rook, whiteTeam);
        ConvertAndPutPiece(x + 1, y + 10, ChessPieceType.Rook, whiteTeam);
        ConvertAndPutPiece(x, y + 11, ChessPieceType.Bishop, whiteTeam);

    }

    private void ConvertAndPutPiece(int x, int y, ChessPieceType type, int team)
    {
        chessPieces[y, TILE_COUNT_X - x - 1] = SpawnSinglePiece(type, team);
    }

    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        if (type == ChessPieceType.Pawn)
        {
            cp.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()[1].material = teamMaterials[team];
            return cp;
        }
        if (type == ChessPieceType.Rook)
        {
            cp.setScale(0.022f);
            cp.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()[0].material = teamMaterials[team];
            return cp;
        }

        if (type == ChessPieceType.Bishop)
        {
            cp.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()[0].material = teamMaterials[team];
            cp.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()[1].material = teamMaterials[team];
            cp.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()[2].material = teamMaterials[team];
            cp.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()[3].material = teamMaterials[team];
            return cp;
        }

        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    //Positioning
    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    PositionSinglePiece(x, y, true);
                }
            }
        }
    }

    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }

    private Vector3 GetTileCenter(int x, int y)
        => new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);

    //Highlight tiles
    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }

    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }

        availableMoves.Clear();
    }

    //Checkmate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }

    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }

    public void OnResetButton()
    {
        //UI
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        //Fields reset
        currentlyDragging = null;
        availableMoves = new List<Vector2Int>();

        //Clean up
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    Destroy(chessPieces[x, y].gameObject);
                }
                chessPieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
        {
            Destroy(deadWhites[i].gameObject);
        }
        for (int i = 0; i < deadBlacks.Count; i++)
        {
            Destroy(deadBlacks[i].gameObject);
        }

        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
    }

    public void OnExitButton()
    {
        Application.Quit();
    }

    //Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2 pos)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
            {
                return true;
            }
        }

        return false;
    }

    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if (!ContainsValidMove(ref availableMoves, new Vector2(x, y)))
        {
            return false;
        }

        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);


        //Is there another piece on the target position?
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if (cp.team == ocp.team)
            {
                return false;
            }

            //If it's the enemy team
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                {
                    CheckMate(1);
                }

                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) 
                    - bounds 
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                {
                    CheckMate(0);
                }

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y);

        isWhiteTurn = !isWhiteTurn;

        CheckForRemainingPieces();

        return true;
    }

    private void CheckForRemainingPieces()
    {
        int w = 0;
        int b = 0;
        for(int i = 0; i < TILE_COUNT_X; i++)
            for(int j = 0; j < TILE_COUNT_Y; j++)
                if(chessPieces[i,j] != null)
                {
                    if (chessPieces[i, j].team == 0)
                        w++;
                    else
                        b++;
                }

        if(w == 0)
        {
            CheckMate(1);
        }
        if(b == 0)
        {
            CheckMate(0);
        }
    }

    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (tiles[x, y] == hitInfo)
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return -Vector2Int.one; // returns -1 -1, which is invalid
    }
}
