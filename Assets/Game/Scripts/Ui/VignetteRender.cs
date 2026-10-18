using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class VignetteRender : MonoBehaviour
{
    
    public static VignetteRender Instance { get; private set; }
    
    [SerializeField] Color color = new Color(0.6f, 0f, 0f, 1f);
    [SerializeField, Range(0.1f, 0.9f)] float innerRadius = 0.4f;
    [SerializeField] float smoothTime = 0.25f;
    [SerializeField, Range(0f, 1f)] float maxAlpha = 0.9f;
    
    [SerializeField] AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Low-health pulse")]
    [SerializeField] float pulseSpeed = 4f;
    [SerializeField] float pulseAmount = 0.1f;
    [SerializeField, Range(0f, 1f)] float pulseBelowHealth = 0.3f;

    Image image;
    float current, velocity, target, health01 = 1f;

    void Awake()
    {
        Instance = this;
        
        image = GetComponent<Image>();
        image.raycastTarget = false;

        var tex = MakeVignetteTexture(256);
        image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        image.type = Image.Type.Simple;
        image.color = new Color(1f, 1f, 1f, 0f);
    }
    
    public void SetHealth(float health, float maxHealth)
    {
        health01 = Mathf.Clamp01(health / maxHealth);
        target = intensityCurve.Evaluate(health01);
    }

    void Update()
    {
        current = Mathf.SmoothDamp(current, target, ref velocity, smoothTime);

        float pulse = 0f;
        if (health01 < pulseBelowHealth)
            pulse = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * pulseAmount;

        var c = image.color;
        c.a = Mathf.Clamp01(current * maxAlpha + pulse);
        image.color = c;
    }

    Texture2D MakeVignetteTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var center = new Vector2(0.5f, 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var uv = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
            float d = Vector2.Distance(uv, center) / 0.7071f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, 1f, d));
            tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
        }
        tex.Apply();
        return tex;
    }
    
}
