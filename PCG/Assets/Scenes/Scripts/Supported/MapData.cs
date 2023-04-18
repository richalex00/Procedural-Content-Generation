using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapData
{
    private string _name;
    private int[,] _gridMap;
    public int[,] GridMap
    {
        get
        {
            return _gridMap;
        }
        set
        {
            _gridMap = null;
            _gridMap = value;
        }
    }

    public MapData(string name, int[,] gridMap)
    {
        _name = name;
        _gridMap = gridMap;
    }
}
