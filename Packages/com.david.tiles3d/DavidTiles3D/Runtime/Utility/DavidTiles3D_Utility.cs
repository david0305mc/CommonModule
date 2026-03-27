using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;

namespace DavidTiles3D
{
    [InitializeOnLoad]
    public static class DavidTiles3D_Utility
    {

        static DavidTiles3D_Utility()
        {
            UpdateAllTileGroupNames();
            if (DavidTiles3D_Settings.EditorInstance.UseUndoAPI)
                ClearUndoStack();
        }
        private static Dictionary<int, DavidTiles3D_Tile> _cache = new Dictionary<int, DavidTiles3D_Tile>();
        private static Dictionary<string, DavidTiles3D_Tile> _tagCache = new Dictionary<string, DavidTiles3D_Tile>();
        private static Dictionary<string, DavidTiles3D_TileGroup> _groupCache = new Dictionary<string, DavidTiles3D_TileGroup>();

        private static DavidTiles3D_TileGroup[] _gCache = null;
        public static DavidTiles3D_TileGroup[] Groups
        {
            get
            {
                if (_gCache == null)
                    _gCache = Resources.LoadAll<DavidTiles3D_TileGroup>("");
                return _gCache;
            }
        }

        public static DavidTiles3D_Tile GetTile(int tileID)
        {
            DavidTiles3D_Tile tile;

            //try id cache
            if (_cache.TryGetValue(tileID, out tile))
            {
                if (tile != null)
                    return tile;
                _cache.Remove(tileID);
            }

            foreach (var g in Groups)
            {
                tile = g.GetTile(tileID);
                if (tile != null)
                {
                    _cache.Add(tileID, tile);
                    return tile;
                }
            }
            return null;
        }
        public static DavidTiles3D_Tile GetTile(int tileID, string name, string group)
        {
            DavidTiles3D_Tile tile;

            //try id cache
            if (_cache.TryGetValue(tileID, out tile))
            {
                if (tile != null)
                    return tile;
                _cache.Remove(tileID);
            }

            string tag = group + name;
            //try tag cache
            if (_tagCache.TryGetValue(tag, out tile))
            {
                if (tile != null)
                    return tile;
                _tagCache.Remove(tag);
            }

            //get group
            DavidTiles3D_TileGroup tileGroup = GetGroup(group);

            if (tileGroup != null)
            {
                //try id first
                tile = tileGroup.GetTile(tileID);
                if (tile != null)
                {
                    _cache.Add(tileID, tile);
                    return tile;
                }

                //try tag next
                tile = tileGroup.GetTile(name);
                if (tile != null)
                {
                    _tagCache.Add(tag, tile);
                    return tile;
                }
            }

            //try looking just via ID one more time
            if (tileID != -1)
            {
                return GetTile(tileID);
            }

            return null;
        }

        public static DavidTiles3D_TileGroup GetGroup(string name)
        {
            DavidTiles3D_TileGroup group = null;

            if (string.IsNullOrEmpty(name))
                return null;

            if (_groupCache.TryGetValue(name, out group))
            {
                if (group != null)
                    return group;
                _groupCache.Remove(name);
            }

            foreach (var g in Groups)
            {
                if (g == null)
                    continue;
                if (g.name == name)
                {
                    _groupCache.Add(g.name, g);
                    return g;
                }
            }
            return null;
        }

        public static void ClearCache(int tileID)
        {
            if (_cache.TryGetValue(tileID, out DavidTiles3D_Tile tile))
            {
                _cache.Remove(tileID);
                if (_tagCache.ContainsKey(tile.Tag))
                {
                    _tagCache.Remove(tile.Tag);
                }
            }
            //clear group cache for good measure
            _gCache = null;
        }

        public static void UpdateAllTileGroupNames()
        {
            var groups = Resources.LoadAll<DavidTiles3D_TileGroup>("");
            foreach (var group in groups)
            {
                group.UpdateTilesWithGroupName();
            }
        }

        public static void ClearUndoStack()
        {
            Undo.ClearAll();
        }

        public static List<DavidTiles3D_TileGroup> LoadTileGroups()
        {
            EnsureFolders();
            return Resources.LoadAll<DavidTiles3D_TileGroup>("").ToList();
        }

        public static DavidTiles3D_TileGroup GetTileGroup(string groupName)
        {
            //todo
            return null;
        }

        public static void RepairBlocks(DavidTiles3D_BlockBehaviour block, List<DavidTiles3D_BlockBehaviour> siblings, string tilegroup, string tilename, int tileID = -1)
        {
            if (block == null || block.Anchor == null)
                return;


            var tile = DavidTiles3D_Utility.GetTile(tileID, tilename, tilegroup);
            if (tile != null)
            {
                int missingTileId = block.TileID;

                //group name might be not set on the tile, so make sure to set again!
                tile.SetGroupName(tilegroup);
                //find all blocks with same old tile info with missing links in the scene and update them as well
                var brokenBlocks = siblings.
                    Where(b => b.TileID == missingTileId && b.GetTile() == null).ToList();

                int i;
                int successes = 0;
                for (i = 0; i < brokenBlocks.Count(); i++)
                {
                    brokenBlocks[i].UpdateTileInfo(tile);
                    var node = brokenBlocks[i].GetInternalNode();
                    if (node != null)
                    {
                        node.UpdateTileInfo(tile.TileID, tile.Name, tile.Group);
                        node.ResetInstance(); //this will also update the block witht the new tile info
                        successes++;
                    }
                    else
                    {
                        Debug.LogError("Missing internal node");
                    }
                }

                Debug.Log($"DavidTiles3D: Successfully linked {successes} block(s) with tile {tilename} again");
            }
            else
            {
                Debug.Log($"No tile with name {tilename} on group {tilegroup} existing!");
            }
        }

        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder($"Assets/DavidTiles3D"))
                AssetDatabase.CreateFolder("Assets", "DavidTiles3D");

            if (!AssetDatabase.IsValidFolder($"Assets/DavidTiles3D/Resources"))
                AssetDatabase.CreateFolder("Assets/DavidTiles3D", "Resources");
        }

        public static bool DoesTileExist(int tileId, string name, string group)
        {
            return GetTile(tileId, name, group) != null;
        }

        public static GUIStyle RichStyle
        {
            get
            {
                var style = new GUIStyle(GUI.skin.label);
                style.wordWrap = true;
                style.richText = true;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleLeft;
                return style;
            }
        }
    }
}


#endif
