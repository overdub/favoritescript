using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "FavoritesData", menuName = "Favorites/Favorites Data", order = 1)]
public class FavoritesData : ScriptableObject {
    [Tooltip("List of favorite pages.")]
    public List<FavoritePage> pages = new List<FavoritePage>();
}

[System.Serializable]
public class FavoritePage {
    [Tooltip("List of favorite objects for this page.")]
    public List<Object> favorites = new List<Object>();
}
