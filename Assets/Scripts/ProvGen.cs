using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI;

public class ProvGen : MonoBehaviour
{
    public ComputeShader provGen;
    public int provinceCellSize;
    public float waterLevel; //0.0725
    public Texture2D boundaries;
    public Texture2D heightmap;
    
    public TMP_InputField waterLevelInput;
    public TMP_Text cellSizeText;
    public Slider cellSizeSlider;
    public TMP_Text progressText;

    public Toggle randomizeColors;

    public void Update()
    {
        cellSizeText.text = "Cell Size: " + cellSizeSlider.value.ToString();
    }

    public void StartGen()
    {
        provinceCellSize = (int)cellSizeSlider.value;
        waterLevel = float.Parse(waterLevelInput.text) / 255f;
        heightmap = new Texture2D(2, 2);
        heightmap.LoadImage(File.ReadAllBytes(Application.dataPath + "/Input/heightmap.png"));
        boundaries = new Texture2D(2, 2);
        boundaries.LoadImage(File.ReadAllBytes(Application.dataPath + "/Input/boundaries.png"));
        StartCoroutine(GenerateProvinces());
    }

    public IEnumerator GenerateProvinces()
    {
        progressText.text = "Generating";
        progressText.color = Color.red;
        RenderTexture provMap = new RenderTexture(heightmap.width, heightmap.height, 1);
        provMap.enableRandomWrite = true;
        provMap.Create();
        RenderTexture cutMap = new RenderTexture(heightmap.width, heightmap.height, 1);
        cutMap.enableRandomWrite = true;
        cutMap.Create();
        ComputeBuffer cellPositions =
            new((heightmap.width / provinceCellSize + 3) * (heightmap.height / provinceCellSize + 3), sizeof(float) * 2);
        ComputeBuffer cellIndices =
            new(heightmap.width * heightmap.height, sizeof(int));

        //Split
        provGen.SetInt("CellSize", provinceCellSize);
        provGen.SetInt("Width", heightmap.width / provinceCellSize + 1);
        provGen.SetInt("PixelWidth", heightmap.width);
        provGen.SetInt("PixelHeight", heightmap.height);
        provGen.SetFloat("WaterHeight", waterLevel);
        provGen.SetVector("Offset", new Vector2(Random.Range(0f, 999999f), Random.Range(0f, 999999f)));
        provGen.SetTexture(0, "Result", provMap);
        provGen.SetTexture(0, "Heightmap", heightmap);
        provGen.SetBuffer(0, "CellPositions", cellPositions);
        provGen.SetBuffer(0, "Indices", cellIndices);
        provGen.Dispatch(0, heightmap.width / 8, heightmap.height / 8, 1);

        //Borders
        provGen.SetTexture(1, "Cut", cutMap);
        provGen.SetTexture(1, "BorderMap", boundaries);
        provGen.SetTexture(1, "Result", provMap);
        provGen.SetBuffer(1, "CellPositions", cellPositions);
        provGen.SetBuffer(1, "Indices", cellIndices);
        provGen.SetTexture(1, "Heightmap", heightmap);
        provGen.Dispatch(1, heightmap.width / 8, heightmap.height / 8,
            provinceCellSize * 2 / 8 + 1);

        yield return null;

        bool done = false;
        int rep = 0;
        while (!done)
        {
            RenderTexture combined = new RenderTexture(heightmap.width, heightmap.height, 1);
            combined.enableRandomWrite = true;
            combined.Create();
            ComputeBuffer doneBuffer = new(1, sizeof(int));
            int[] b = new int[] { 1 };
            doneBuffer.SetData(b);

            //Recombine
            provGen.SetBuffer(2, "Done", doneBuffer);
            provGen.SetTexture(2, "Result", provMap);
            provGen.SetTexture(2, "Cut", cutMap);
            provGen.SetTexture(2, "Combined", combined);
            provGen.SetTexture(2, "BorderMap", boundaries);
            provGen.Dispatch(2, heightmap.width / 8, heightmap.height / 8, 1);
            doneBuffer.GetData(b);
            done = b[0] == 1;

            //Apply
            provGen.SetTexture(3, "Result", provMap);
            provGen.SetTexture(3, "Cut", cutMap);
            provGen.SetTexture(3, "Combined", combined);
            provGen.Dispatch(3, heightmap.width / 8, heightmap.height / 8, 1);

            yield return null;
            Debug.Log(rep);
            rep++;
            doneBuffer.Dispose();
            combined.Release();
        }

        if (randomizeColors.isOn)
        {
            int numColors = (heightmap.width / provinceCellSize + 2) * (heightmap.height / provinceCellSize + 2);
            ComputeBuffer colorBuffer = new(numColors, sizeof(float) * 4);
            HashSet<Color> colors = new HashSet<Color>();
            for (int i = 0; i < numColors; i++)
            {
                Color col = new Color(Random.Range(100, 255) / 255f, Random.Range(100, 255) / 255f, Random.Range(100, 255) / 255f);
                while (colors.Contains(col))
                {
                    col = new Color(Random.Range(100, 255) / 255f, Random.Range(1, 255) / 255f, Random.Range(100, 255) / 255f);
                }
                colors.Add(col);
            }
            Color[] colorsArr = new Color[colors.Count];
            colors.CopyTo(colorsArr);
            colorBuffer.SetData(colorsArr);

            yield return null;

            provGen.SetTexture(4, "Result", provMap);
            provGen.SetBuffer(4, "ColorBuffer", colorBuffer);
            provGen.Dispatch(4, heightmap.width / 8, heightmap.height / 8, 1);

            colorBuffer.Dispose();

            yield return null;
        }

        RenderTexture.active = provMap;
        Texture2D provMapTex2D = new(heightmap.width, heightmap.height);
        provMapTex2D.ReadPixels(new Rect(0, 0, heightmap.width, heightmap.height), 0, 0);
        RenderTexture.active = null;

        File.WriteAllBytes(Application.dataPath + "/Output/provinces.png", provMapTex2D.EncodeToPNG());
        cellPositions.Dispose();
        cellIndices.Dispose();
        Debug.Log("Done!");
        progressText.text = "Done";
        progressText.color = Color.green;
    }
}
