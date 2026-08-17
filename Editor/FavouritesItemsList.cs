using System.Collections.Generic;
using UnityEngine;

namespace ShoreRat.Editor.Favourites
{
    internal class FavouritesItemsList : ScriptableObject
    {
        [SerializeField] private List<FavouritesItemEntry> _entries = new();

        public List<FavouritesItemEntry> Entries => _entries;
    }
}
