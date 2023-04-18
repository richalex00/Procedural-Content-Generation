using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.Runtime.Serialization.Formatters.Binary;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] private ObjectGenerator generateObjects;

    // Map Settings
    [Header("Grid map size")]
    [Tooltip("Specifies the size of the grid e.g. 2 = 2x2 grid of maps")]
    [Range(1, 4)]
    public int gridSize;

    [Header("Size of the individual maps")]
    public Vector3Int mapSize;

    // Terrain layer information
    [System.Serializable]
    public class TerrainLayer
    {
        public Tilemap tilemap;
        [Tooltip("Scripted tile is added here")]
        public ScriptedTile tile;
        [Range(0, 100)]
        [Tooltip("Chance for a tile to appear on the sublayer")]
        public int saturation;
        [Range(0, 5)]
        [Tooltip("Minimum distance from this layer and the sublayer's sublayer")]
        public int distance; // Also the tile must be surrounded on all corners by sublayer to appear
        [Range(0, 10)]
        [Tooltip("The amount of variation of saturation between each map in the grid (plus/minus)")]
        public float variation;
        [Range(0, 10)]
        [Tooltip("Strength of cellular automaton")]
        public int cellular; // layer 0 -> 0, layer 1 -> 10, extra layers 0-2.
    }

    [SerializeField]
    private TerrainLayer[] layers;

    private bool islands = true;
    private int mainLayer = 1;
    private int backLayer = 0;

    [Header("Islands")]
    [SerializeField]
    [Tooltip("Minimum number of tiles in an island")]
    private int minimumLandTiles;

    [SerializeField]
    [Tooltip("Minimum number of tiles needed to keep background layer")]
    private int minimumWaterTiles;

    // Cellular automaton limits
    private int birthLimit = 4;
    private int deathLimit = 4;
    private int birthLimitX = 4;
    private int deathLimitX = 4;

    private int[,] gridMap;

    public int[,] GridMap
    {
        get => gridMap;
        set => gridMap = value;
    }

    public struct Coord
    {
        public int x;
        public int y;

        public Coord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    private int[,] terrainMap;
    private int level = 0;
    private int saveCount = 0;
    private int initChance;
    [HideInInspector]
    public int width;
    [HideInInspector]
    public int height;
    private bool hasStarted = false;

    // islandInfo[x,y] is a double array to get the island of specific tile
    // islandList provides a list of List,coord., each list containing all the coordinates of a island: Struct Coord{int x; int y;}
    private int[,] islandInfo;

    public int[,] IslandInfo => islandInfo;

    private List<List<Coord>> islandList;

    public List<List<Coord>> IslandList => islandList;

    //Update used to enable user input
    //new map with "Space" key, save map with "S" key, load map with "M" key
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Selects tile on mouse button down and prints position and island information in the log
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Tilemap touchedTilemap = null;
            Vector3Int gridPos = Vector3Int.zero;
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                gridPos = layers[i].tilemap.WorldToCell(mousePos);
                TileBase touchedTile = layers[i].tilemap.GetTile(gridPos);
                if (touchedTile)
                {
                    touchedTilemap = layers[i].tilemap;
                    break;
                }
            }

            if (touchedTilemap)
            {
                if (islands)
                {
                    int x = -gridPos.x + ((gridSize * mapSize.x) / 2);
                    int y = -gridPos.y + ((gridSize * mapSize.y) / 2);
                    Debug.Log("Island number: " + islandInfo[x, y]);
                }
                Debug.Log(touchedTilemap.GetTile(gridPos) + " selected at position: " + gridPos);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateNewMap();
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (hasStarted)
            {
                ClearTileMaps(true);
                generateObjects.DestroyAllObjects();
            }
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            if (GridMap != null)
            {
                SaveMap();
            }
            else
            {
                Debug.Log("Map not saved");
            }
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            // change map to correct name for loading
            LoadMap("Assets/SavedMaps" + "Map");
        }
    }


    //utility functions
    public void GenerateNewMap()
    {
        if (hasStarted)
        {
            ClearTileMaps(true);
            generateObjects.DestroyAllObjects();
        }

        MakeGridMap();

        if (islands)
        {
            IslandGenerator();
        }

        PrintMap();

        if (layers.Length >= 2)
        {
            generateObjects.ObjGenerator(gridMap);
        }

        hasStarted = true;
    }

    //generates a new map and prints on tilemap
    public void MakeGridMap()
    {
        // Save map tile information with the int GridMap variable
        gridMap = new int[gridSize * mapSize.x, gridSize * mapSize.y];

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                int[,] newMap = GenerateTerrainMap();
                int startX = i * mapSize.x;
                int startY = j * mapSize.y;

                for (int x = 0; x < mapSize.x; x++)
                {
                    for (int y = 0; y < mapSize.y; y++)
                    {
                        int gridX = startX + x;
                        int gridY = startY + y;
                        GridMap[gridX, gridY] = newMap[x, y];
                    }
                }
            }
        }
    }


    // Generates islands using map saved in GridMap variable
    public void IslandGenerator()
    {
        if (GridMap == null)
        {
            return;
        }

        int islandIndex = 1;
        islandInfo = new int[gridSize * mapSize.x, gridSize * mapSize.y];
        islandList = new List<List<Coord>>();

        // Process back layer islands
        List<List<Coord>> backlayerIslands = GetIslands(backLayer);
        foreach (List<Coord> backIsland in backlayerIslands)
        {
            if (backIsland.Count < minimumWaterTiles)
            {
                foreach (Coord coord in backIsland)
                {
                    GridMap[coord.x, coord.y] = backLayer + 1;
                }
            }
        }

        // Process main layer islands
        List<List<Coord>> allIslands = GetIslands(mainLayer);
        foreach (List<Coord> island in allIslands)
        {
            if (island.Count < minimumLandTiles)
            {
                foreach (Coord coord in island)
                {
                    GridMap[coord.x, coord.y] = mainLayer - 1;
                }
            }
            else
            {
                islandList.Add(island);
            }
        }

        // Assign island info
        foreach (List<Coord> island in islandList)
        {
            foreach (Coord coord in island)
            {
                islandInfo[coord.x, coord.y] = islandIndex;
            }
            islandIndex++;
        }
    }


    // Saves current map in a serialized binary file
    public void SaveMap()
    {
        string directoryPath = "Assets/SavedMaps/";
        string filePath = GenerateUniqueDataFileName(directoryPath);

        if (filePath == null)
        {
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            Debug.Log("Directory created: " + directoryPath);
            Directory.CreateDirectory(directoryPath);
        }

        using (var file = File.OpenWrite(filePath))
        {
            var data = new MapData(filePath, GridMap);
            var formatter = new BinaryFormatter();
            formatter.Serialize(file, data);
        }

        Debug.Log("Map saved to: " + filePath);
    }


    // Loads serialized binary file
    public void LoadMap(string mapPath)
    {
        if (!File.Exists(mapPath))
        {
            Debug.LogError("File not found");
            return;
        }

        using (var file = File.OpenRead(mapPath))
        {
            var formatter = new BinaryFormatter();
            var data = (MapData)formatter.Deserialize(file);
            GridMap = data.GridMap;
        }

        generateObjects.DestroyAllObjects();
        PrintMap();
        generateObjects.ObjGenerator(GridMap);
    }


    // Prints the current map (2D array) in the GridMap variable onto the tilemaps
    public void PrintMap()
    {
        int width = gridMap.GetLength(0);
        int height = gridMap.GetLength(1);
        int centerX = width / 2;
        int centerY = height / 2;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = gridMap[x, y];
                int posX = -x + centerX;
                int posY = -y + centerY;
                PrintOnTileMaps(index, posX, posY);
            }
        }
    }


    // Prints a tile from layer[index] at position (x,y) on the screen
    public void PrintOnTileMaps(int index, int x, int y)
    {
        int randomIndex = Random.Range(1, 21); // Generate a random index for tile selection
        for (int l = 1; l < layers.Length; l++)
        {
            if (index >= l)
            {
                TileBase tile = layers[l].tile.tile[randomIndex % layers[l].tile.tile.GetLength(0)];
                layers[l].tilemap.SetTile(new Vector3Int(x, y, 0), tile); // Set the tile on the tilemap
            }
        }
        TileBase baseTile = layers[0].tile.tile[randomIndex % layers[0].tile.tile.GetLength(0)];
        layers[0].tilemap.SetTile(new Vector3Int(x, y, 0), baseTile); // Set the base tile on the tilemap
    }

    // Clears all the tiles from the tilemaps
    // Does not destroy GameObject list, they are dealt with in objectGenerator
    public void ClearTileMaps(bool complete)
    {
        foreach (var layer in layers)
        {
            layer.tilemap.ClearAllTiles();
        }

        if (complete)
        {
            terrainMap = null;
        }
    }


    public void ClearTileInMap(Vector3Int pos)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            layers[i].tilemap.SetTile(pos, null);
        }
    }


    // Island generation
    // Checks if position is outside the grid
    private bool IsMapTileValid(int x, int y)
    {
        int maxX = gridSize * mapSize.x;
        int maxY = gridSize * mapSize.y;
        return x >= 0 && x < maxX && y >= 0 && y < maxY;
    }


    // Flood Fill algorithm --> used in the generation of Islands
    // Islands are essentially lists of coordinates which are defined to be bound to each other
    // MakeIslands returns a list of coordinates that share the same base level as each other and are touching
    // We have the starting position (startX, startY) of a new island. The flood fill algorithm is used to identify the other tiles that are bound to it
    private List<Coord> MakeIslands(int startX, int startY)
    {
        List<Coord> island = new List<Coord>(); // Creates a new list of coordinates
        int[,] mapTemp = new int[gridSize * mapSize.x, gridSize * mapSize.y]; // Temporary 2d array of tiles, so we don't look twice the same tile
        int tileType = GridMap[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY)); // Coordinates will be stored in a queue, starting with our first coordinates
        mapTemp[startX, startY] = 1;

        while (queue.Count > 0) // As long as we have tiles in the queue, we add them to the list and check the neighbors
        {
            Coord tile = queue.Dequeue(); // The queue is emptied of current coordinates
            island.Add(tile); // The current coordinates are added to the island
            for (int x = tile.x - 1; x <= tile.x + 1; x++) // For loops are used to check all neighboring tiles of the current coordinates (FLOOD FILL)
            {
                for (int y = tile.y - 1; y <= tile.y + 1; y++)
                {
                    if (IsMapTileValid(x, y) && (x == tile.x || y == tile.y))
                    {
                        if (mapTemp[x, y] == 0 && GridMap[x, y] == tileType) // If this neighbor has not been checked and tiletype is valid, its coordinates are added to the queue
                        {
                            mapTemp[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }
        return island;
    }


    // Creates the list of islands. A new island is created each time we stumble upon the correct layer (tileType) that has been chosen
    private List<List<Coord>> GetIslands(int tileType)
    {
        List<List<Coord>> islands = new List<List<Coord>>(); // Creates a list of islands (islands are lists of coordinates, so it's a List of lists)
        int[,] mapTemp = new int[gridSize * mapSize.x, gridSize * mapSize.y]; // A temporary 2D array to store the info of tiles that are already in a island, to not count them twice

        for (int x = 0; x < gridSize * mapSize.x; x++)
        {
            for (int y = 0; y < gridSize * mapSize.y; y++)
            {
                if (mapTemp[x, y] == 0 && GridMap[x, y] == tileType) // If tile is of correct type and not already in an island, we can create a new island
                {
                    List<Coord> newIsland = MakeIslands(x, y); // Make island will use a flood fill algorithm to create a list of coordinates
                    islands.Add(newIsland); // An island is added to the list
                    foreach (Coord tile in newIsland) // We keep track in mapTemp of coordinates that are already in an Island
                    {
                        mapTemp[tile.x, tile.y] = 1;
                    }
                }
            }
        }
        return islands;
    }


    private string GenerateUniqueDataFileName(string directory)
    {
        string name = directory + "Map" + saveCount.ToString() + "-" + System.DateTime.Now.ToString("MMdd_HHmmss");
        saveCount++;
        if (saveCount > 512)
        {
            Debug.Log("Create Data File failed: Too many files.");
            return null;
        }
        if (File.Exists(name))
        {
            return GenerateUniqueDataFileName(directory);
        }

        return name;
    }


    private int[,] GenerateTerrainMap()
    {
        level = 0;
        initChance = layers[1].saturation + (int)Random.Range(-layers[1].variation, layers[1].variation);
        terrainMap = null;
        width = mapSize.x;
        height = mapSize.y;

        if (terrainMap == null)
        {
            terrainMap = new int[width, height];
            Initialize();
        }

        // Generate terrain map for layer 1
        for (int i = 0; i < layers[1].cellular; i++)
        {
            terrainMap = GenerateTilePositions(terrainMap); // We repeat the perlin noise effect many times to create a nice organic map generation
        }
        level++;

        // Generate terrain maps for additional layers
        //Apply cellular automaton and InitializeExtra for other layers
        for (int i = 2; i < layers.Length; i++)
        {
            InitializeExtra();
            for (int j = 0; j < layers[i].cellular; j++)
            {
                terrainMap = GenerateLayer(terrainMap);
            }
            level++;
        }

        return terrainMap;
    }


    // Cellular automaton
    // This function uses the Cellular Automata Algorithm to create a Perlin noise effect
    // We already have a 2D array (oldmap[height, width]), on which we iterate the Cellular automata process
    // With Cellular Automata, we compare each tile to its neighbors
    private int[,] GenerateTilePositions(int[,] oldMap)
    {
        int[,] newMap = new int[width, height];
        int neighbor;
        BoundsInt myNeighbors = new BoundsInt(-1, -1, 0, 3, 3, 1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                neighbor = 0;
                foreach (var b in myNeighbors.allPositionsWithin)
                {
                    if (b.x == 0 && b.y == 0)
                        continue;
                    if (x + b.x >= 0 && x + b.x < width && y + b.y >= 0 && y + b.y < height)
                    {
                        neighbor += oldMap[x + b.x, y + b.y];
                    }
                }

                newMap[x, y] = (oldMap[x, y] == 1 && neighbor >= deathLimit) || (oldMap[x, y] == 0 && neighbor > birthLimit) ? 1 : 0;
            }
        }
        return newMap;
    }


    // This algorithm can generate multiple layers, except for the first 2 layers, other layers will be generated on top of the highest layer only 
    private int[,] GenerateLayer(int[,] oldmap)
    {
        int[,] newMap = new int[width, height];
        int neighb;
        BoundsInt myNeighbs = new BoundsInt(-1, -1, 0, 3, 3, 1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (oldmap[x, y] < level)
                {
                    newMap[x, y] = oldmap[x, y]; // Keep the data for layers below current level
                }
                else // Cellular Automata
                {
                    neighb = 0;
                    foreach (var b in myNeighbs.allPositionsWithin)
                    {
                        if (b.x == 0 && b.y == 0)
                            continue;
                        if (x + b.x >= 0 && x + b.x < width && y + b.y >= 0 && y + b.y < height)
                        {
                            if (oldmap[x, y] == oldmap[x + b.x, y + b.y])
                                neighb++;
                            if (oldmap[x, y] == level && oldmap[x + b.x, y + b.y] < level)
                                neighb++;
                        }
                    }

                    newMap[x, y] = (oldmap[x, y] == level + 1 && neighb > birthLimitX) || (oldmap[x, y] == level && neighb < deathLimitX) ? level + 1 : level;
                }
            }
        }
        return newMap;
    }


    // Generates random numbers from 0 to 1 in a 2D array to initialize the first random data
    private void Initialize()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                terrainMap[x, y] = Random.Range(1, 101) < initChance ? 1 : 0; // Set terrainMap[x, y] to 1 or 0 based on initChance
            }
        }
    }


    // Generates random numbers in a 2D array to initialize the first random data
    private void InitializeExtra()
    {
        int tileDistance = layers[level + 1].distance;
        int initializationChance = layers[level + 1].saturation + (int)Random.Range(-layers[level + 1].variation, layers[level + 1].variation);

        for (int x = tileDistance + 1; x < width - tileDistance - 1; x++)
        {
            for (int y = tileDistance + 1; y < height - tileDistance - 1; y++)
            {
                if (terrainMap[x, y] == level &&
                    terrainMap[x - tileDistance, y - tileDistance] >= level &&
                    terrainMap[x - tileDistance, y + tileDistance] >= level &&
                    terrainMap[x + tileDistance, y - tileDistance] >= level &&
                    terrainMap[x + tileDistance, y + tileDistance] >= level)
                {
                    terrainMap[x, y] = Random.Range(1, 101) < layers[level + 1].saturation ? level + 1 : level;
                }
            }
        }
    }

    // Check for errors and warnings
    private bool ErrorLog()
    {
        if (layers.Length < 2)
        {
            Debug.Log("Error: Map Generator needs at least 2 terrain layers");
            return true;
        }

        if (layers[0].saturation != 0)
        {
            Debug.Log("Warning: First layer is always fully saturated");
        }

        return false;
    }

}
