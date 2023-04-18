using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ObjData
{
    public GameObject Obj;
    [Range(0, 1)]
    [Tooltip("Layer on which the object is generated.")]
    public int layer;

    [HideInInspector]
    public bool exclusive;

    [Range(0, 100)]
    [Tooltip("Chances of spawning on the layer previously chosen")]
    public int objSaturation;

    [Range(0, 5)]
    [Tooltip("Minimum distance between the object and sublayer tiles. Recommended : 1")]
    public int objDistance;

    public ObjData(GameObject obj, int layer, int objSaturation, int objDistance)
    {
        Obj = obj;
        this.layer = layer;
        exclusive = true; //Set default value for exclusive
        this.objSaturation = objSaturation;
        this.objDistance = objDistance;
    }
}

public class ObjectGenerator : MonoBehaviour
{
    public ObjData[] objectData;
    public MapGenerator mapGenerator;
    private List<GameObject> objectList;

    public List<GameObject> ObjectList
    {
        get
        {
            return objectList;
        }
        set
        {
            DestroyAllObjects();
            objectList = value;
        }
    }

    // This method takes a 2D map and instantiates objects randomly depending on the layer type
    public void ObjGenerator(int[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);
        objectList = new List<GameObject>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                foreach (var objectDatum in objectData)
                {
                    if (map[x, y] == objectDatum.layer && x > objectDatum.objDistance + 1 && x < width - objectDatum.objDistance - 1
                        && y > objectDatum.objDistance + 1 && y < height - objectDatum.objDistance - 1)
                    {
                        if (objectDatum.objDistance == 0)
                        {
                            if (Random.Range(1, 101) < objectDatum.objSaturation)
                            {
                                objectList.Add(Instantiate(objectDatum.Obj, new Vector3(-x + width / 2, -y + height / 2, 0), Quaternion.identity));
                                if (objectDatum.exclusive)
                                    break;
                            }
                        }
                        else if (map[x - objectDatum.objDistance, y - objectDatum.objDistance] >= objectDatum.layer
                                 && map[x - objectDatum.objDistance, y + objectDatum.objDistance] >= objectDatum.layer
                                 && map[x + objectDatum.objDistance, y - objectDatum.objDistance] >= objectDatum.layer
                                 && map[x + objectDatum.objDistance, y + objectDatum.objDistance] >= objectDatum.layer)
                        {
                            if (Random.Range(1, 101) < objectDatum.objSaturation)
                            {
                                objectList.Add(Instantiate(objectDatum.Obj, new Vector3(-x + width / 2, -y + height / 2, 0), Quaternion.identity));
                                if (objectDatum.exclusive)
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }


    // This method destroys all objects in the objectList
    public void DestroyAllObjects()
    {
        if (objectList != null && objectList.Count > 0)
        {
            foreach (var obj in objectList)
            {
                if (obj != null)
                    Destroy(obj);
            }
            objectList.Clear();
        }
    }
}
