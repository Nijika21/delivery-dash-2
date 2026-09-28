using UnityEngine;

public class TruckSkinCatalog : ScriptableObject
{
    public int Version;
    public string SourceHash;
    public Sprite[] Skins;
    public Sprite Get(int index)=>Skins!=null && index>=0 && index<Skins.Length?Skins[index]:null;
}
