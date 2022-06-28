using UnityEngine;
using UnityEditor;
namespace Mahrq
{
    /// <summary>
    /// Author:         Mark Mendoza
    /// 
    /// Description:    Editor window tool for level creation, speeding up the pipeline
    ///                 for multiple levels.
    ///                 The tool takes a png image and reads its pixel co-ordinance(color and position) which
    ///                 correlates to a specific GameObject prefab(defined by the user) and position to be spawned in the scene view.
    ///                 
    ///                 Best used on modular prefabs that have that same sizing.
    ///                 
    /// Date:           04/09/2019
    ///                 19/09/2019 - Reads alpha channel to determine inital spawn rotation of the prefab.
    ///                 27/06/2022 - Added more options, styling and tool tips.
    ///                 28/06/2022 - Added save preset feature.
    /// 
    /// Notes:          PNG setup when importing into asset folder:
    ///                 1 - Enable read and writing in advanced dropdown
    ///                 2 - Change compression to none.
    ///                 3 - Enable Aplha is Transparency.
    ///                 This ensures that the colors retain their value when being imported
    ///                 
    /// Bugs:           When loading a preset, you won't be able to change any of the Color Code properties unless
    ///                 you close the editor window or click the + to add an element.
    ///                 You can click the - to revert to what you had just before.
    ///                 
    ///                 !Preset color code values are lost when re-opening the unity editor! to fix soon.
    ///                 
    ///                 Null referance errors in play mode. Just reload the editor window. 
    /// </summary>
    public class PNGLevelGeneratorEditor : EditorWindow
    {
        [SerializeField]
        private string presetName = "Preset 001";
        [SerializeField]
        private string levelName = "New Level";
        private GameObject levelHolder;
        [SerializeField]
        private float spacing;
        [SerializeField]
        private BuildCoordinate buildCoordinates;
        [SerializeField]
        private bool affectRotation;
        [SerializeField]
        private RotationAxis affectedAxis;
        [SerializeField]
        private Texture2D pngMapToScan;
        [SerializeField]
        private ColorToPrefab[] colorToPrefab;
        [SerializeField]
        private LevelGeneratorEditorSaveData[] savedPresets;

        private ScriptableObject target;
        private SerializedObject serializedObject;
        private SerializedProperty colorCode;
        private SerializedProperty savedPresetsProperty;

        private Vector2 scrollPos;

        [SerializeField]
        private string[] options;
        [SerializeField]
        private int savedPresetindex = 0;
        private int overwriteWarning;
        private int previousOverwriteIndex = 0;
        private int deleteWarning;
        private int previousDeleteIndex = -1;

        [MenuItem("Mahrq/Level Generator/PNG Scanner")]
        static void StartWindow()
        {
            PNGLevelGeneratorEditor levelGeneratorEditor = GetWindow<PNGLevelGeneratorEditor>();
            levelGeneratorEditor.Show();
        }
        /// <summary>
        /// Initialise serialised objects and properties and load a previous state if used before.
        /// </summary>
        private void OnEnable()
        {
            //Load saved state of the window if it exist.
            if (EditorPrefs.HasKey("pngToLevel"))
            {
                string savedItems = EditorPrefs.GetString("pngToLevel", JsonUtility.ToJson(this, false));
                JsonUtility.FromJsonOverwrite(savedItems, this);
            }
            //Serialize ColorToPrefab
            target = this;
            serializedObject = new SerializedObject(target);
            colorCode = serializedObject.FindProperty("colorToPrefab");
            savedPresetsProperty = serializedObject.FindProperty("savedPresets");

            InitialiseEditorStyling();
            InitialiseToolTips();
            //Hardcoding 10 preset slots
            if (options.Length < 10)
            {
                options = new string[10];
                
            }
            if (savedPresets.Length < 10)
            {
                savedPresets = new LevelGeneratorEditorSaveData[10];
            }
            options = GetPresetCollectionOptions(savedPresets);
        }
        /// <summary>
        /// Save the input fields of the window when closing.
        /// This ensures that when reopening the window, the values are not reset.
        /// </summary>
        private void OnDisable()
        {
            string savedItems = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString("pngToLevel", savedItems);
        }
        private void OnGUI()
        {
            serializedObject.Update();
            //Dynamic scroll bar appears when window can't contain all the input fields.
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
            EditorGUILayout.BeginVertical(editorSkin.scrollView);
            //Title
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Png To Level Generator", s_title);
            EditorGUILayout.Space(16);
            EditorGUILayout.BeginHorizontal();
            //Preset Name
            EditorGUILayout.PrefixLabel(presetNameToolTip, editorSkin.textField, editorSkin.label);
            presetName = EditorGUILayout.TextField(presetName, editorSkin.textField);
            GUILayout.FlexibleSpace();
            //Save Button
            if (GUILayout.Button("Save", s_smallButtons))
            {
                int currentIndex = savedPresetindex;
                //Check if current slot is empty otherwise save immediately to this slot.
                if (PresetSlotOccupied)
                {
                    //Check if this is the second time that this button was clicked while on the same slot.
                    if (previousOverwriteIndex != currentIndex)
                    {
                        overwriteWarning = 0;
                        previousOverwriteIndex = currentIndex;
                    }
                    overwriteWarning++;
                    //Overwrite slot on second click.
                    if (overwriteWarning > 1)
                    {
                        overwriteWarning = 0;
                        savedPresets[currentIndex].SaveData(presetName, levelName, spacing, buildCoordinates, affectRotation, affectedAxis, pngMapToScan, colorToPrefab);
                        options[currentIndex] = savedPresets[currentIndex]._presetName;
                        Debug.Log($"Preset: {options[currentIndex]} overwritten.");
                    }
                    //Warn on first click when overwriting
                    else
                    {
                        Debug.LogWarning($"You are trying to overwrite preset: {options[currentIndex]}" +
                                        $"\nClick Save again to overwrite.");
                    }
                }
                else
                {
                    if (PresetNameAlreadyExists(presetName, ref currentIndex))
                    {
						savedPresetindex = currentIndex;
                        Debug.LogWarning($"Preset: {presetName} already exists" +
                            $"\nSwitching to {presetName} preset slot.");
                    }
                    else
                    {
                        savedPresets[currentIndex].SaveData(presetName, levelName, spacing, buildCoordinates, affectRotation, affectedAxis, pngMapToScan, colorToPrefab);
                        options[currentIndex] = savedPresets[currentIndex]._presetName;
                    }
                }
                options = GetPresetCollectionOptions(savedPresets);
                EditorGUI.FocusTextInControl(null);
            }
            EditorGUILayout.Space(8);
            //Load Button
            if (GUILayout.Button("Load", s_smallButtons))
            {
                int currentIndex = savedPresetindex;
                if (PresetSlotOccupied)
                {
                    LevelGeneratorEditorSaveData data = savedPresets[currentIndex].LoadData();
                    presetName = data._presetName;
                    levelName = data._levelName;
                    spacing = data._spacing;
                    buildCoordinates = data._buildCoordinates;
                    affectRotation = data._affectRotation;
                    affectedAxis = data._affectedAxis;
                    pngMapToScan = data._pngMapToScan;
                    colorToPrefab = data._colorToPrefab;
                    Debug.Log($"Preset: {presetName} loaded");
                }
                EditorGUI.FocusTextInControl(null);
            }
            //Preset slots
            savedPresetindex = EditorGUILayout.Popup(savedPresetindex, options, editorSkin.customStyles[0]);
            //Delete
            if (GUILayout.Button("Delete", s_smallButtons))
            {
                int currentIndex = savedPresetindex;
                if (PresetSlotOccupied)
                {
                    if (previousDeleteIndex != currentIndex)
                    {
                        deleteWarning = 0;
                        previousDeleteIndex = currentIndex;
                    }
                    deleteWarning++;
                    if (deleteWarning > 1)
                    {
                        deleteWarning = 0;
                        string clearedPresetname = savedPresets[currentIndex]._presetName;
                        savedPresets[currentIndex].ClearData();
                        if (!PresetSlotOccupied)
                        {
                            Debug.Log($"Preset: {clearedPresetname} deleted.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"You are trying to delete preset: {options[currentIndex]}" +
                                        $"\nClick Delete again to confirm.");
                    }
                }
                options = GetPresetCollectionOptions(savedPresets);
                EditorGUI.FocusTextInControl(null);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(16);
            //Level Name
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(levelNameToolTip, editorSkin.textField, editorSkin.label);
            levelName = EditorGUILayout.TextField(levelName, editorSkin.textField);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            //Prefab Spacing
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(prefabSpacingToolTip, editorSkin.textField, editorSkin.label);
            spacing = EditorGUILayout.FloatField(spacing, editorSkin.textField);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            //Build Coordinate
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(buildCoordinatesToolTip, editorSkin.textField, editorSkin.label);
            buildCoordinates = (BuildCoordinate)EditorGUILayout.EnumPopup(buildCoordinates, editorSkin.customStyles[0]);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            //Affect Rotation
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(affectRotationToolTip, editorSkin.textField, editorSkin.label);
            affectRotation = EditorGUILayout.Toggle(affectRotation, editorSkin.customStyles[1]);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            //Affect Rotation Axis
            EditorGUILayout.BeginHorizontal();
            if (!affectRotation)
            {
                affectedAxis = 0;
            }
            EditorGUILayout.PrefixLabel(affectedAxisToolTip, editorSkin.textField, editorSkin.label);
            affectedAxis = (RotationAxis)EditorGUILayout.EnumFlagsField(affectedAxis, editorSkin.customStyles[0]);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            //Level Layout
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(levelLayoutToolTip, editorSkin.textField, editorSkin.label);
            pngMapToScan = (Texture2D)EditorGUILayout.ObjectField(pngMapToScan, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            //Input field for color codes
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(prefabColorCodeToolTip, editorSkin.textField, editorSkin.label);
            EditorGUILayout.PropertyField(colorCode, prefabColorCodeOverrideName, true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(32);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            //Button to generate level
            if (GUILayout.Button("Generate Level", editorSkin.button))
            {
                if (pngMapToScan != null && colorToPrefab.Length > 0)
                {
                    GenerateLevel(pngMapToScan);
                }
                else
                {
                    Debug.LogError("Unassigned properties for PNGLevelGeneratorEditor");
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        /// <summary>
        /// Generates a level by reading each pixel color of the image and using that information 
        /// to set the position of a defined prefab that will spawn into the scene.
        /// 
        /// Arguments:
        ///     -PNG(preferably) image to be read. The image must not be compressed and allows to be read/written to.
        /// </summary>           
        private void GenerateLevel(Texture2D map)
        {
            //Create empty parent GameObject to hold the level
            levelHolder = new GameObject(levelName);
            Color pixelColor;
            Vector3 spawnLocation;
            Quaternion spawnRotation = Quaternion.identity;
            //Iterate through each pixel of the texture map to read its color.
            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++)
                {
                    pixelColor = map.GetPixel(x, y);
                    //Continue loop if fully transparent.
                    if (pixelColor.a == 0f)
                    {
                        continue;
                    }
                    if (affectRotation)
                    {
                        //Determine orientation of object depending on alpha value.
                        if (pixelColor.a > 0.9f)
                        {
                            spawnRotation = Quaternion.identity;
                        }
                        else if (pixelColor.a > 0.8f)
                        {
                            spawnRotation = Quaternion.Euler(GetSpawnRotation(affectedAxis, 90f));
                        }
                        else if (pixelColor.a > 0.7f)
                        {
                            spawnRotation = Quaternion.Euler(GetSpawnRotation(affectedAxis, 180f));
                        }
                        else if (pixelColor.a > 0.5f)
                        {
                            spawnRotation = Quaternion.Euler(GetSpawnRotation(affectedAxis, 270f));
                        }
                        else
                        {
                            spawnRotation = Quaternion.identity;
                        }
                    }
                    //Iterate through the colorToPrefab array and see if the pixel color matches the color stored in the array.
                    for (int i = 0; i < colorToPrefab.Length; i++)
                    {
                        if (RGBMatch(pixelColor, colorToPrefab[i].color))
                        {
                            if (!affectRotation)
                            {
                                Vector3 prefabRotation = colorToPrefab[i].prefab.transform.rotation.eulerAngles;
                                spawnRotation = Quaternion.Euler(prefabRotation);
                            }
                            spawnLocation = GetSpawnPosition(buildCoordinates, (float)x, (float)y, spacing);
                            //Instantiate object and set its parent to the empty GameObject.
                            (Instantiate(colorToPrefab[i].prefab, spawnLocation, spawnRotation) as GameObject).transform.parent = levelHolder.transform;
                        }
                    }
                }
            }
        }
        private bool RGBMatch(Color input, Color target)
        {
            if (input.r.Equals(target.r) && input.g.Equals(target.g) && input.b.Equals(target.b))
            {
                return true;
            }
            return false;
        }
        private Vector3 GetSpawnPosition(BuildCoordinate coordinates, float x, float y, float spacing)
        {
            float multiplier = spacing;
            if (spacing == 0f)
            {
                multiplier = 1f;
            }
            switch (coordinates)
            {
                case BuildCoordinate.xy:
                    return new Vector3(x, y, 0) * multiplier;
                case BuildCoordinate.xz:
                    return new Vector3(x, 0, y) * multiplier;
                case BuildCoordinate.yz:
                    return new Vector3(0, x, y) * multiplier;
                default:
                    return Vector3.zero;
            }
        }
        private string[] GetPresetCollectionOptions(LevelGeneratorEditorSaveData[] collection)
        {
            string[] options = new string[collection.Length];
            try
            {
                string name;
                for (int i = 0; i < collection.Length; i++)
                {
                    name = collection[i]._presetName;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = $"[Empty {i}]";
                    }
                    options[i] = name;
                }
            }
            catch (System.IndexOutOfRangeException e)
            {
                Debug.LogError(e.Message);
            }
            return options;
        }
        //Reads binary representation of the axis parameter and returns a vector of the given rotation value and axis affected.
        private Vector3 GetSpawnRotation(RotationAxis axis, float rotationValue)
        {
            Vector3 result = Vector3.zero;
            string binary = System.Convert.ToString((int)axis, 2);
            char[] formattedBinary = binary.ToCharArray();
            System.Array.Reverse(formattedBinary);
            string correctedBinary = new System.String(formattedBinary);
            float[] xyz = new float[3];
            for (int i = formattedBinary.Length; i < xyz.Length; i++)
            {
                correctedBinary += "0";
            }
            try
            {
                for (int i = 0; i < correctedBinary.Length; i++)
                {
                    if (correctedBinary[i] == '1')
                    {
                        xyz[i] = rotationValue;
                    }
                    else
                    {
                        xyz[i] = 0f;
                    }
                }
                result.x = xyz[0];
                result.y = xyz[1];
                result.z = xyz[2];
            }
            catch (System.IndexOutOfRangeException e)
            {
                Debug.LogError(e.Message);
            }
            return result;
        }
        #region Styling
        private GUISkin editorSkin;
        private GUIStyle s_title;
        private GUIStyle s_smallButtons;
        private void InitialiseEditorStyling()
        {
            Color pink = new Color(1f, 162f/255f, 230f/255f, 200f/255f);
            Color magenta = new Color(1f, 0f, 1f, 188f/255f);
            //Title
            s_title = GUIStyle.none;
            s_title.normal.textColor = magenta;
            s_title.hover.textColor = magenta;
            s_title.active.textColor = magenta;
            s_title.focused.textColor = magenta;
            s_title.onNormal.textColor = magenta;
            s_title.onHover.textColor = magenta;
            s_title.onActive.textColor = magenta;
            s_title.onFocused.textColor = magenta;
            s_title.fontSize = 16;
            s_title.fontStyle = FontStyle.BoldAndItalic;
            s_title.alignment = TextAnchor.MiddleLeft;
            //Skin
            editorSkin = ScriptableObject.CreateInstance<GUISkin>();
            editorSkin.label.normal.textColor = pink;
            editorSkin.label.hover.textColor = pink;
            editorSkin.label.active.textColor = pink;
            editorSkin.label.focused.textColor = pink;
            editorSkin.label.onNormal.textColor = pink;
            editorSkin.label.onHover.textColor = pink;
            editorSkin.label.onActive.textColor = pink;
            editorSkin.label.onFocused.textColor = pink;
            editorSkin.label.fontStyle = FontStyle.Bold;
            editorSkin.label.alignment = TextAnchor.MiddleLeft;
            //TextField
            editorSkin.textField.normal.textColor = pink;
            editorSkin.textField.hover.textColor = pink;
            editorSkin.textField.active.textColor = pink;
            editorSkin.textField.focused.textColor = pink;
            editorSkin.textField.onNormal.textColor = pink;
            editorSkin.textField.onHover.textColor = pink;
            editorSkin.textField.onActive.textColor = pink;
            editorSkin.textField.onFocused.textColor = pink;
            editorSkin.textField.alignment = TextAnchor.MiddleLeft;
            editorSkin.textField.padding = new RectOffset(5, 0, 0, 0);
            //Scroll View
            editorSkin.scrollView.padding = new RectOffset(10, 5, 0, 0);
            //Button
            editorSkin.button.normal.textColor = pink;
            editorSkin.button.hover.textColor = pink;
            editorSkin.button.active.textColor = magenta;
            editorSkin.button.focused.textColor = pink;
            editorSkin.button.onNormal.textColor = pink;
            editorSkin.button.onHover.textColor = pink;
            editorSkin.button.onActive.textColor = magenta;
            editorSkin.button.onFocused.textColor = pink;
            editorSkin.button.fontSize = 14;
            editorSkin.button.fontStyle = FontStyle.Bold;
            editorSkin.button.alignment = TextAnchor.MiddleCenter;
            editorSkin.button.fixedHeight = 30f;
            editorSkin.button.fixedWidth = 120f;
            //Small Button
            s_smallButtons = new GUIStyle(editorSkin.button);
            s_smallButtons.fontSize = 11;
            s_smallButtons.fontStyle = FontStyle.Bold;
            s_smallButtons.fixedHeight = 20f;
            s_smallButtons.fixedWidth = 50f;
            //Popup menu
            editorSkin.customStyles = new GUIStyle[2];
            editorSkin.customStyles[0] = new GUIStyle(EditorStyles.popup);
            editorSkin.customStyles[0].normal.textColor = pink;
            editorSkin.customStyles[0].hover.textColor = pink;
            editorSkin.customStyles[0].active.textColor = pink;
            editorSkin.customStyles[0].focused.textColor = pink;
            editorSkin.customStyles[0].onNormal.textColor = pink;
            editorSkin.customStyles[0].onHover.textColor = pink;
            editorSkin.customStyles[0].onActive.textColor = pink;
            editorSkin.customStyles[0].onFocused.textColor = pink;
            editorSkin.customStyles[0].alignment = TextAnchor.MiddleLeft;
            editorSkin.customStyles[0].padding = new RectOffset(5, 0, 0, 0);
            //Toggle
            editorSkin.customStyles[1] = new GUIStyle(EditorStyles.toggle);

            editorSkin.settings.selectionColor = pink;
            editorSkin.settings.cursorColor = magenta;
        }
        private GUIContent presetNameToolTip;
        private GUIContent levelNameToolTip;
        private GUIContent prefabSpacingToolTip;
        private GUIContent buildCoordinatesToolTip;
        private GUIContent affectRotationToolTip;
        private GUIContent affectedAxisToolTip;
        private GUIContent levelLayoutToolTip;
        private GUIContent prefabColorCodeToolTip;
        private GUIContent prefabColorCodeOverrideName;
        private void InitialiseToolTips()
        {
            presetNameToolTip = new GUIContent("Preset Name", "The name of this current configuration.");
            levelNameToolTip = new GUIContent("Name", "The instantiated gameobject will have this name.");
            prefabSpacingToolTip = new GUIContent("Spacing", "Spacing between each prefab spawned.");
            buildCoordinatesToolTip = new GUIContent("Build Coordinates", "The two(2) axis the level would be built around");
            affectRotationToolTip = new GUIContent("Affect Rotation", "True: reads aplha values to determine spawn rotation." +
                "\nvalue > 0.9 = Quaternion.Identity" +
                "\nvalue > 0.8 = 90°" +
                "\nvalue > 0.7 = 180°" +
                "\nvalue > 0.5 = -90°" +
                "\nFalse: Uses the prefab's rotation");
            affectedAxisToolTip = new GUIContent("Affected Axis", "Which axis to rotate the prefab based on their transform rotation");
            levelLayoutToolTip = new GUIContent("Layout",
            "2D Image representation of the level layout." +
            "\nTo ensure best quality, use a PNG Image with the following setup:" +
            "\n- Enable Aplha is Transparency." +
            "\n- Enable read and writing in advanced dropdown." +
            "\n -Change compression to none.");
            prefabColorCodeToolTip = new GUIContent("Prefab Color Code", "The color representation of the prefab you want to spawn" +
                " based on the 2d image given.");
            prefabColorCodeOverrideName = new GUIContent("", "The color representation of the prefab you want to spawn based on the 2d image given.");
        }
        #endregion
        private bool PresetNameAlreadyExists(string input, ref int redirectIndex)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == input)
                {
                    redirectIndex = i;
                    return true;
                }
            }       
            return false;
        }
        private bool PresetSlotOccupied => options[savedPresetindex] != "[Empty]" && !string.IsNullOrEmpty(savedPresets[savedPresetindex]._presetName) 
                    && options[savedPresetindex] == savedPresets[savedPresetindex]._presetName;

        public enum BuildCoordinate
        {
            xy,
            xz,
            yz
        }
        [System.Flags]
        public enum RotationAxis
        {
            None = 0,
            X = 1,
            Y = 1 << 1,
            Z = 1 << 2
        }
        /// <summary>
        /// Class data that stores a color to match a prefab GameObject.
        /// </summary>
        [System.Serializable]
        public class ColorToPrefab
        {
            public Color color;
            public GameObject prefab;
        }
        [System.Serializable]
        public class LevelGeneratorEditorSaveData
        {
            public string _presetName;
            public string _levelName;
            public float _spacing;
            public BuildCoordinate _buildCoordinates;
            public bool _affectRotation;
            public RotationAxis _affectedAxis;
            public Texture2D _pngMapToScan;
            public ColorToPrefab[] _colorToPrefab;

            public LevelGeneratorEditorSaveData()
            {
                _presetName = "";
                _levelName = "";
                _spacing = 0f;
                _buildCoordinates = 0;
                _affectRotation = false;
                _affectedAxis = 0;
                _pngMapToScan = null;
                _colorToPrefab = null;
            }
            public void SaveData(string presetName, string levelName, float spacing, BuildCoordinate buildCoordinates, bool affectRotation,
                                    RotationAxis affectedAxis, Texture2D pngMapToScan, ColorToPrefab[] colorToPrefab )
            {
                _presetName = presetName;
                _levelName = levelName;
                _spacing = spacing;
                _buildCoordinates = buildCoordinates;
                _affectRotation = affectRotation;
                _affectedAxis = affectedAxis;
                _pngMapToScan = pngMapToScan;
                _colorToPrefab = colorToPrefab;
            }
            public LevelGeneratorEditorSaveData LoadData()
            {
                return this;
            }
            public void ClearData()
            {
                _presetName = "";
                _levelName = "";
                _spacing = 0f;
                _buildCoordinates = 0;
                _affectRotation = false;
                _affectedAxis = 0;
                _pngMapToScan = null;
                _colorToPrefab = null;
            }
        }
    }
}
