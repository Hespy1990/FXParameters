using FX.Patterns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace FX
{
    public class TagConfiguration
    {
        public string type { get; set; }
        public List<Tag> tags { get; set; }

        public TagConfiguration(string type)
        {
            this.type = type;
            this.tags = new List<Tag>();
        }
    }

    public class Tag
    {
        public string id { get; set; }
        public string value { get; set; }

        public Tag(string id, string value)
        {
            this.id = id;
            this.value = value;
        }
    }

    public class Scene
    {
        public string Name { get; set; }
        public string OriginalName { get; set; }

        public List<string> TagIds { get; set; }

        public Scene(string name)
        {
            Name = name;
            OriginalName = name; 
            TagIds = new List<string>();
        }

        public bool AddTag(string tagId)
        {
            if (!TagIds.Contains(tagId))
            {
                TagIds.Add(tagId);
                return true;
            }
            return false;
        }

        public bool RemoveTag(string tagId)
        {
            return TagIds.Remove(tagId);
        }
    }

    public class FXSceneManager : MonoBehaviour
    {
        FXManager fXManager;
        [HideInInspector]
        public List<Scene> scenes;
        [HideInInspector]
        public List<TagConfiguration> tagConfigurations;

        public bool exportParameterListOnStart = false;

        private Scene currentScene;
        public Scene CurrentScene
        {
            get => currentScene;
            set
            {
                if (currentScene != value)
                {
                    currentScene = value;
                    onCurrentSceneChanged?.Invoke(currentScene);
                    onCurrentSceneNameChanged?.Invoke(currentScene.Name);

                }
            }
        }

        public delegate void OnSceneListUpdated(List<Scene> scenes);
        public event OnSceneListUpdated onSceneListUpdated;

        public delegate void OnCurrentSceneChanged(Scene newScene);
        public event OnCurrentSceneChanged onCurrentSceneChanged;

        public delegate void OnCurrentSceneNameChanged(string name);
        public event OnCurrentSceneNameChanged onCurrentSceneNameChanged;

        public delegate void OnSceneRemoved(string sceneName);
        public event OnSceneRemoved onSceneRemoved;

        public delegate void OnTagConfigurationUpdated(List<TagConfiguration> tagConfigurations);
        public event OnTagConfigurationUpdated onTagConfigurationUpdated;

        private void Awake()
        {
            fXManager = FXManager.Instance;
            scenes = new List<Scene>();
            tagConfigurations = LoadTagConfigurations();
            PopulateScenesList();
        }

        private void Start()
        {

            string directoryPath = Path.Combine(Application.streamingAssetsPath, "FX");
            string filePath = Path.Combine(directoryPath, "DefaultGroups.json");

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> {
                        new ColourHandler()
                    },
                };

                List<FXGroupData> fxGroupPresets = JsonConvert.DeserializeObject<List<FXGroupData>>(json, settings);
                foreach (var group in fxGroupPresets) {
                    fXManager.CreateGroup(group);
                }
            }   

            if (exportParameterListOnStart) ExportParameterList();
            CreateNewScene();
            onTagConfigurationUpdated?.Invoke(tagConfigurations);

        }

        public void PopulateScenesList()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            string scenesFolderPath = Path.Combine(Application.streamingAssetsPath, "FX Scenes");

            if (Directory.Exists(scenesFolderPath))
            {
                DirectoryInfo scenesDirectory = new DirectoryInfo(scenesFolderPath);
                FileInfo[] sceneFiles = scenesDirectory.GetFiles("*.json");

                HashSet<string> sceneNamesInDirectory = new HashSet<string>(
                    sceneFiles
                        .Where(file => file.Name != "ParameterList")
                        .Select(file => Path.GetFileNameWithoutExtension(file.Name))
                );

                scenes.RemoveAll(scene => !sceneNamesInDirectory.Contains(scene.Name));

                foreach (FileInfo file in sceneFiles)
                {
                    if (file.Name != "ParameterList")
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(file.Name);
                        Scene existingScene = scenes.Find(scene => scene.Name == sceneName);

                        if (existingScene == null)
                        {
                            Scene newScene = new Scene(sceneName);

                            string json = File.ReadAllText(file.FullName);
                            var settings = new JsonSerializerSettings { Converters = new List<JsonConverter> { new ColourHandler() } };
                            FX.FXManager.FXData preset = JsonConvert.DeserializeObject<FX.FXManager.FXData>(json, settings);
                            newScene.TagIds = preset.sceneTagIds;
                            
                            scenes.Add(newScene);
                        }
                    }
                }

                stopwatch.Stop();
                Debug.Log($"PopulateScenesList (without tags) took: {stopwatch.ElapsedMilliseconds} ms");
                

                onSceneListUpdated?.Invoke(scenes);
            }
            else
            {
                Debug.LogError("Scenes folder not found: " + scenesFolderPath);
                stopwatch.Stop();
            }
        }


        public bool LoadScene(string name)
        {
            var sceneExists = scenes.Any(s => s.Name == name);

            if (!sceneExists)
            {
                // TODO - we should probably PopulateScenesList here
                Debug.LogError($"Scene '{name}' does not exist in the list of scenes.");
                return false;
            }

            if (fXManager.LoadScene(name, out List<string> loadedTagIds))
            {
                CurrentScene = scenes.Find(s => s.Name == name);

                if (CurrentScene != null)
                {
                    CurrentScene.OriginalName = name;
                    // Only add tags which exist in the tagConfigurations
                    var validTagIds = loadedTagIds
                        .Where(tagId => tagConfigurations.Any(tc => tc.tags.Any(t => t.id == tagId)))
                        .ToList();

                    var unknownTagIds = loadedTagIds
                        .Where(tagId => !tagConfigurations.Any(tc => tc.tags.Any(t => t.id == tagId)))
                        .ToList();

                    if (unknownTagIds.Any())
                    {
                        Debug.LogWarning($"The following tags in scene '{name}' are not recognized: {string.Join(", ", unknownTagIds)}");
                    }

                    CurrentScene.TagIds = validTagIds;
                    onCurrentSceneChanged?.Invoke(currentScene);
                    return true;
                }
            }
            return false;
        }

        public void SaveScene()
        {
            if (CurrentScene != null)
            {
                SaveScene(CurrentScene);
            }
        }

        public void SaveScene(FX.Scene scene)
        {

            if (scene.Name != scene.OriginalName)
            {
                string scenesFolderPath = Path.Combine(Application.streamingAssetsPath, "FX Scenes");
                string oldScenePath = Path.Combine(scenesFolderPath, scene.OriginalName + ".json");
                string oldMetaPath = oldScenePath + ".meta";

                if (File.Exists(oldScenePath))
                {
                    try
                    {
                        File.Delete(oldScenePath);
                        if (File.Exists(oldMetaPath))
                        {
                            File.Delete(oldMetaPath);
                        }
                        scene.OriginalName = scene.Name;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Failed to delete old scene file: " + ex.Message);
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning("Old scene file does not exist: " + oldScenePath);
                }
            }

            fXManager.SaveScene(scene);
            PopulateScenesList();
        }

        public void SaveCurrentSceneAs(string newName)
        {
            if (CurrentScene != null)
            {
                string scenesFolderPath = Path.Combine(Application.streamingAssetsPath, "FX Scenes");
                string newScenePath = Path.Combine(scenesFolderPath, newName + ".json");

                if (File.Exists(newScenePath))
                {
                    Debug.LogWarning($"A scene with the name '{newName}' already exists.");
                    return;
                }

                try
                {
                    string originalName = CurrentScene.Name;
                    CurrentScene.Name = newName;
                    CurrentScene.OriginalName = newName;

                    fXManager.SaveScene(CurrentScene);

                    //CurrentScene.Name = originalName;
                    //CurrentScene.OriginalName = originalName;

                    PopulateScenesList();
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to save scene with new name: " + ex.Message);
                }
            }
        }



        public void ExportParameterList()
        {
            Scene parameterListScene = new Scene("ParameterList");
            fXManager.SaveScene(parameterListScene, true);
        }

        public void RemoveScene(string name)
        {
            string scenesFolderPath = Path.Combine(Application.streamingAssetsPath, "FX Scenes");
            string scenePath = Path.Combine(scenesFolderPath, name + ".json");
            string metaPath = Path.Combine(scenePath + ".meta");

            if (File.Exists(scenePath))
            {
                File.Delete(scenePath);
                PopulateScenesList();

                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
                onSceneRemoved?.Invoke(name);
            }
            else
            {
                Debug.LogError("Scene not found: " + name);
            }
        }

        public void ResetCurrentScene()
        {
            if (CurrentScene != null)
            {
                LoadScene(CurrentScene.Name);
            }
        }

        public void CreateNewScene(string name = null)
        {
            GroupFXController[] allGroups = GameObject.FindObjectsOfType<GroupFXController>();

            foreach (var group in allGroups)
            {
                if (!group.isPinned)
                {
                    fXManager.RemoveGroup(group.address);                   
                }
                else
                {
                    group.ClearFXAdresses();
                }
            }

            fXManager.ResetAllParamsToDefault();
            currentScene = new Scene(string.IsNullOrEmpty(name) ? "Untitled" : name)
            {
                OriginalName = string.IsNullOrEmpty(name) ? "Untitled" : name
            };

            if (!string.IsNullOrEmpty(name)) SaveScene();

            onCurrentSceneChanged?.Invoke(currentScene);
        }

        public bool AddTagToConfiguration(string type, string value)
        {
            var tagConfig = tagConfigurations.Find(tc => tc.type == type);
            if (tagConfig != null && !tagConfig.tags.Exists(t => t.value == value))
            {
                string id = Guid.NewGuid().ToString();
                tagConfig.tags.Add(new Tag(id, value));
                SaveTagConfigurations();
                return true;
            }
            return false;
        }

        public bool RemoveTagFromConfiguration(string tagID)
        {
            foreach (var config in tagConfigurations) {
                var tag = config.tags.Find(t => t.id == tagID);
                if (tag != null)
                {
                    config.tags.Remove(tag);
                    SaveTagConfigurations();
                    return true;
                    
                }
            }
            return false;
        }

        public bool RemoveTagFromConfiguration(string type, string tagID)
        {
            var tagConfig = tagConfigurations.Find(tc => tc.type == type);
            if (tagConfig != null)
            {
                var tag = tagConfig.tags.Find(t => t.id == tagID);
                if (tag != null)
                {
                    tagConfig.tags.Remove(tag);
                    SaveTagConfigurations();
                    return true;
                }
            }
            return false;
        }

        public bool SetTag(Tag tagData)
        {
            foreach (var config in tagConfigurations)
            {
                var tag = config.tags.Find(t => t.id == tagData.id);
                if (tag != null)
                {
                    tag.value = tagData.value;
                    SaveTagConfigurations();
                    return true;

                }
            }
            return false;
        }

        public bool UpdateTag(string tagID, string value)
        {
            foreach (var config in tagConfigurations)
            {
                var tag = config.tags.Find(t => t.id == tagID);
                if (tag != null)
                {
                    tag.value = value;
                    SaveTagConfigurations();
                    return true;

                }
            }
            return false;
        }

        public bool AddTagToCurrentScene(string tagId)
        {
            return AddTagToScene(CurrentScene.Name, tagId);
        }

        public bool AddTagToScene(string sceneName, string tagId)
        {
            Debug.Log($"Attempting to add tag to scene: {sceneName}, tag ID: {tagId}");

            Scene scene = scenes.Find(s => s.Name == sceneName);
            if (scene == null)
            {
                Debug.LogError($"Scene not found: {sceneName}");
                return false;
            }

            var tag = tagConfigurations.SelectMany(tc => tc.tags).FirstOrDefault(t => t.id == tagId);
            if (tag == null)
            {
                Debug.LogError($"Tag not found: {tagId}");
                return false;
            }

            Debug.Log($"Adding tag {tagId} to scene {sceneName}");
            bool addedOK = scene.AddTag(tagId);
            if (addedOK && scene.Name == currentScene.Name) onCurrentSceneChanged.Invoke(currentScene);   
            return addedOK;
        }

        public void RemoveAllTagsFromCurrentScene(string tagType = null)
        {
            if (tagType == null)
            {
                CurrentScene.TagIds.Clear();
                onCurrentSceneChanged?.Invoke(currentScene);
            }
            else
            {
                var tagsToRemove = tagConfigurations
                    .Where(tc => tc.type == tagType)
                    .SelectMany(tc => tc.tags)
                    .Select(t => t.id)
                    .ToList();

                CurrentScene.TagIds.RemoveAll(tagId => tagsToRemove.Contains(tagId));
                onCurrentSceneChanged?.Invoke(currentScene);
            }
        }


        public bool RemoveTagFromCurrentScene(string tagId)
        {
            return RemoveTagFromScene(CurrentScene.Name, tagId);
        }

        public bool RemoveTagFromScene(string sceneName, string tagId)
        {
            Debug.Log($"Attempting to remove tag from scene: {sceneName}, tag ID: {tagId}");

            Scene scene = scenes.Find(s => s.Name == sceneName);
            if (scene == null)
            {
                Debug.LogError($"Scene not found: {sceneName}");
                return false;
            }

            var tag = tagConfigurations.SelectMany(tc => tc.tags).FirstOrDefault(t => t.id == tagId);
            if (tag == null)
            {
                Debug.LogError($"Tag not found: {tagId}");
                return false;
            }

            Debug.Log($"Removing tag {tagId} from scene {sceneName}");
            bool removedOK = scene.RemoveTag(tagId);
            if (removedOK && scene.Name == currentScene.Name) onCurrentSceneChanged.Invoke(currentScene);
            return removedOK;
        }

        private void SaveTagConfigurations()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "FX/TagConfigurations.json");
            var json = JsonConvert.SerializeObject(tagConfigurations, Formatting.Indented);
            File.WriteAllText(path, json);
            onTagConfigurationUpdated.Invoke(tagConfigurations);
        }

        private List<TagConfiguration> LoadTagConfigurations()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "FX/TagConfigurations.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<TagConfiguration>>(json);
            }
            return new List<TagConfiguration>
            {
                new TagConfiguration("scene-bucket"),
                new TagConfiguration("scene-label")
            };
        }

        public List<Scene> FilterScenesByTag(string tagId)
        {
            return scenes.Where(scene => scene.TagIds.Contains(tagId)).ToList();
        }
    }
}
