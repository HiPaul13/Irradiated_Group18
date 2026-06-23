using System;
using UnityEngine;

[Serializable]
public class BookPage
{
    [TextArea(3, 10)]
    public string text;

    public Sprite image;
}
