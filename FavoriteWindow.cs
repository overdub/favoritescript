using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System;
using System.Reflection;
using Object = UnityEngine.Object;

public class FavoriteWindow : EditorWindow {
    [Tooltip("Reference to the favorites data asset.")]
    private FavoritesData favoritesData;
    private const string DATA_ASSET_PATH = "Assets/Editor/FavoritesData.asset";
    private int currentPage = 0;
    private ReorderableList currentPageList;

    [MenuItem("Window/Favorites")]
    public static void ShowWindow() {
        GetWindow<FavoriteWindow>("Favorites");
    }

    private void OnEnable() {
        LoadFavoritesData();
        if (favoritesData.pages == null || favoritesData.pages.Count == 0) {
            favoritesData.pages = new List<FavoritePage> { new FavoritePage() };
            EditorUtility.SetDirty(favoritesData);
        }
        CreateReorderableList();
    }

    // Save any changes once the window is closed.
    private void OnDisable() {
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Loads the FavoritesData asset from disk or creates a new one if it doesn't exist.
    /// </summary>
    private void LoadFavoritesData() {
        favoritesData = AssetDatabase.LoadAssetAtPath<FavoritesData>(DATA_ASSET_PATH);
        if (favoritesData == null) {
            favoritesData = ScriptableObject.CreateInstance<FavoritesData>();
            favoritesData.pages = new List<FavoritePage> { new FavoritePage() };
            AssetDatabase.CreateAsset(favoritesData, DATA_ASSET_PATH);
        }
    }

    /// <summary>
    /// Creates a reorderable list for the current page favorites.
    /// </summary>
    private void CreateReorderableList() {
        if (currentPage < 0 || currentPage >= favoritesData.pages.Count)
            return;

        currentPageList = new ReorderableList(
            favoritesData.pages[currentPage].favorites,
            typeof(Object),
            true, true, false, false
        );

        currentPageList.drawElementCallback = DrawListItem;
        currentPageList.onReorderCallback = list => {
            Debug.Log("List reordered.");
            EditorUtility.SetDirty(favoritesData);
            Repaint();
        };

        currentPageList.elementHeight = 20;
    }

    /// <summary>
    /// Draws each list item as a full‑width button.
    /// A right‑click (detected via MouseDown with button==1) opens a context menu to remove the item.
    /// Clicking (left‑click) the button opens the folder (if it’s a folder) or selects the asset.
    /// </summary>
    private void DrawListItem(Rect rect, int index, bool isActive, bool isFocused) {
        if (index < 0 || index >= favoritesData.pages[currentPage].favorites.Count)
            return;

        Object obj = favoritesData.pages[currentPage].favorites[index];

        // Check for right-click (MouseDown with button 1) on the entire rect.
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition)) {
            GenericMenu menu = new GenericMenu();
            int capturedIndex = index; // capture index for the callback.
            menu.AddItem(new GUIContent("Remove from Favorites"), false, () => RemoveFavorite(capturedIndex));
            menu.ShowAsContext();
            Event.current.Use();
        }

        if (obj == null) {
            EditorGUI.LabelField(rect, "Null");
            return;
        }

        // Use Unity's standard object content (icon + name)
        GUIContent content = EditorGUIUtility.ObjectContent(obj, obj.GetType());

        // The entire rect acts as a button.
        if (GUI.Button(rect, content, EditorStyles.miniButton)) {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path)) {
                // Reload the folder asset fresh to get the proper instance.
                Object folderAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (folderAsset != null) {
                    int folderInstanceID = folderAsset.GetInstanceID();
                    ShowFolderContents(folderInstanceID);
                    EditorUtility.FocusProjectWindow();
                } else {
                    Debug.LogError("Folder asset not found at path: " + path);
                }
            } else {
                Selection.SetActiveObjectWithContext(obj, obj);
            }
        }
    }

    /// <summary>
    /// Removes a favorite from the current page at the given index.
    /// </summary>
    private void RemoveFavorite(int index) {
        favoritesData.pages[currentPage].favorites.RemoveAt(index);
        EditorUtility.SetDirty(favoritesData);
        CreateReorderableList();
    }

    /// <summary>
    /// Selects a folder in the Project window and displays its content using Unity's internal ShowFolderContents method.
    /// This logic is identical to your test script.
    /// </summary>
    /// <param name="folderInstanceID">The instance ID of the folder asset.</param>
    private static void ShowFolderContents(int folderInstanceID) {
        Assembly editorAssembly = typeof(Editor).Assembly;
        System.Type projectBrowserType = editorAssembly.GetType("UnityEditor.ProjectBrowser");

        // Get the internal ShowFolderContents method.
        MethodInfo showFolderContentsMethod = projectBrowserType.GetMethod(
            "ShowFolderContents", BindingFlags.Instance | BindingFlags.NonPublic);

        // Find any open Project Browser windows.
        Object[] projectBrowserInstances = Resources.FindObjectsOfTypeAll(projectBrowserType);

        if (projectBrowserInstances.Length > 0) {
            foreach (var browser in projectBrowserInstances) {
                ShowFolderContentsInternal(browser, showFolderContentsMethod, folderInstanceID);
            }
        } else {
            // Open a new Project Browser if none are open.
            EditorWindow projectBrowser = OpenNewProjectBrowser(projectBrowserType);
            ShowFolderContentsInternal(projectBrowser, showFolderContentsMethod, folderInstanceID);
        }
    }

    private static void ShowFolderContentsInternal(Object projectBrowser, MethodInfo showFolderContentsMethod, int folderInstanceID) {
        // Ensure the Project Browser is in two-column mode.
        SerializedObject serializedObject = new SerializedObject(projectBrowser);
        bool inTwoColumnMode = serializedObject.FindProperty("m_ViewMode").enumValueIndex == 1;

        if (!inTwoColumnMode) {
            MethodInfo setTwoColumns = projectBrowser.GetType().GetMethod(
                "SetTwoColumns", BindingFlags.Instance | BindingFlags.NonPublic);
            setTwoColumns.Invoke(projectBrowser, null);
        }

        bool revealAndFrameInFolderTree = true;
        // Invoke the internal method to show folder contents.
        showFolderContentsMethod.Invoke(projectBrowser, new object[] { folderInstanceID, revealAndFrameInFolderTree });
    }

    private static EditorWindow OpenNewProjectBrowser(System.Type projectBrowserType) {
        EditorWindow projectBrowser = EditorWindow.GetWindow(projectBrowserType);
        projectBrowser.Show();

        // Some initialization is required to avoid NullReferenceExceptions.
        MethodInfo initMethod = projectBrowserType.GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
        initMethod.Invoke(projectBrowser, null);

        return projectBrowser;
    }

    private void OnGUI() {
        Rect dropArea = new Rect(0, 0, position.width, position.height - 40);
        GUI.Box(dropArea, "Drag & Drop Items Here");
        HandleDragAndDrop(dropArea);

        if (currentPageList != null)
            currentPageList.DoList(new Rect(0, 0, position.width, position.height - 40));

        // Bottom navigation area: page navigation and page deletion.
        GUILayout.BeginArea(new Rect(0, position.height - 40, position.width, 40));
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("<", GUILayout.Width(40))) {
            if (currentPage > 0) {
                currentPage--;
                CreateReorderableList();
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Page " + (currentPage + 1) + " / " + favoritesData.pages.Count);
        GUILayout.FlexibleSpace();

        // Delete Page button (only shown if more than one page exists)
        if (favoritesData.pages.Count > 1) {
            if (GUILayout.Button("Delete Page", GUILayout.Width(100))) {
                favoritesData.pages.RemoveAt(currentPage);
                if (currentPage >= favoritesData.pages.Count)
                    currentPage = favoritesData.pages.Count - 1;
                EditorUtility.SetDirty(favoritesData);
                CreateReorderableList();
            }
        }

        if (GUILayout.Button(">", GUILayout.Width(40))) {
            if (currentPage < favoritesData.pages.Count - 1) {
                currentPage++;
            } else {
                favoritesData.pages.Add(new FavoritePage());
                currentPage++;
                EditorUtility.SetDirty(favoritesData);
            }
            CreateReorderableList();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Handles drag and drop into the favorites window.
    /// </summary>
    private void HandleDragAndDrop(Rect dropArea) {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition))
            return;
        switch (evt.type) {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform) {
                    DragAndDrop.AcceptDrag();
                    foreach (var dragged in DragAndDrop.objectReferences) {
                        if (!favoritesData.pages[currentPage].favorites.Contains(dragged)) {
                            favoritesData.pages[currentPage].favorites.Add(dragged);
                            Debug.Log("Added dragged object: " + dragged.name);
                            EditorUtility.SetDirty(favoritesData);
                        }
                    }
                    CreateReorderableList();
                }
                break;
        }
    }
}
