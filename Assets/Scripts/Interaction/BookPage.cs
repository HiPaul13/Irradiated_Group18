using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BookPage
{
    [TextArea(3, 10)]
    public string text;

    [Tooltip("Optional single image. Use Images below when a page needs more than one.")]
    public Sprite image;

    public List<Sprite> images = new List<Sprite>();

    public bool HasImages()
    {
        if (image != null)
            return true;

        if (images == null)
            return false;

        foreach (Sprite sprite in images)
        {
            if (sprite != null)
                return true;
        }

        return false;
    }

    public IEnumerable<Sprite> GetImages()
    {
        if (image != null)
            yield return image;

        if (images == null)
            yield break;

        foreach (Sprite sprite in images)
        {
            if (sprite == null || sprite == image)
                continue;

            yield return sprite;
        }
    }
}
