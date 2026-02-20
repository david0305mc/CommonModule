using System;
using System.Collections.Generic;
using System.Linq;

public static class CollectionUtil
{
    public static bool IsNullOrEmpty<T>(ICollection<T> collection)
    {
        return collection == null || collection.Count == 0;
    }

    public static T GetRandom<T>(IList<T> list)
    {
        if (IsNullOrEmpty(list))
            throw new ArgumentException("List is null or empty.");

        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    public static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }
}