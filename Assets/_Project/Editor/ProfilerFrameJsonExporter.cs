using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public class ProfilerFrameJsonExporter : EditorWindow
{
    [Serializable]
    private class FrameData
    {
        public long frameIndex;
        public long profilerFrameNumber;
        public double cpuFrameMs;
        public double gpuFrameMs;
        public List<ThreadData> threads = new();
    }

    [Serializable]
    private class ThreadData
    {
        public string name;
        public string group;
        public ulong threadId;
        public List<SampleData> samples = new();
    }

    [Serializable]
    private class SampleData
    {
        public string name;
        public string path;
        public string objectName;

        public double totalMs;
        public double selfMs;
        public double totalPercent;
        public double selfPercent;

        public int calls;
        public long gcBytes;
        public int warnings;

        public List<SampleData> children = new();
    }

    [MenuItem("Tools/Profiler/Frame JSON Exporter")]
    private static void Open()
    {
        GetWindow<ProfilerFrameJsonExporter>("Profiler JSON");
    }

    private void OnGUI()
    {
        var profiler = GetProfilerWindow();

        if (profiler == null)
        {
            EditorGUILayout.HelpBox(
                "请先打开 Window > Analysis > Profiler",
                MessageType.Warning);
            return;
        }

        long frameIndex = profiler.selectedFrameIndex;

        EditorGUILayout.LabelField(
            "Selected Frame",
            frameIndex >= 0 ? (frameIndex + 1).ToString() : "None");

        GUI.enabled = frameIndex >= 0;

        if (GUILayout.Button("Export Current Frame"))
        {
            ExportFrame(frameIndex);
        }

        GUI.enabled = true;

        if (GUILayout.Button("Open ProfilerCaptures Folder"))
        {
            string folder = GetCaptureFolder();
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private static ProfilerWindow GetProfilerWindow()
    {
        var windows = Resources.FindObjectsOfTypeAll<ProfilerWindow>();

        return windows.Length > 0 ? windows[0] : null;
    }

    private static void ExportFrame(long frameIndex)
    {
        FrameData output = new()
        {
            frameIndex = frameIndex,
            profilerFrameNumber = frameIndex + 1
        };

        for (int threadIndex = 0; ; threadIndex++)
        {
            using var view =
                ProfilerDriver.GetHierarchyFrameDataView(
                    (int)frameIndex,
                    threadIndex,
                    HierarchyFrameDataView.ViewModes.Default,
                    HierarchyFrameDataView.columnDontSort,
                    false);

            if (!view.valid)
                break;

            if (threadIndex == 0)
            {
                output.cpuFrameMs = view.frameTimeMs;
                output.gpuFrameMs = view.frameGpuTimeMs;
            }

            ThreadData thread = new()
            {
                name = view.threadName,
                group = view.threadGroupName,
                threadId = view.threadId
            };

            var children = new List<int>();
            int root = view.GetRootItemID();

            view.GetItemChildren(root, children);

            foreach (int child in children)
                thread.samples.Add(ReadSample(view, child));

            output.threads.Add(thread);
        }

        string folder = GetCaptureFolder();
        Directory.CreateDirectory(folder);

        string path = Path.Combine(
            folder,
            $"frame_{frameIndex + 1}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        File.WriteAllText(
            path,
            JsonUtility.ToJson(output, true));

        Debug.Log($"Profiler JSON exported:\n{path}");

        EditorUtility.RevealInFinder(path);
    }

    private static SampleData ReadSample(
        HierarchyFrameDataView view,
        int id)
    {
        SampleData sample = new()
        {
            name = view.GetItemName(id),
            path = view.GetItemPath(id),

            objectName = view.GetItemColumnData(
                id,
                HierarchyFrameDataView.columnObjectName),

            totalMs = view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnTotalTime),

            selfMs = view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnSelfTime),

            totalPercent = view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnTotalPercent),

            selfPercent = view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnSelfPercent),

            calls = (int)view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnCalls),

            gcBytes = (long)view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnGcMemory),

            warnings = (int)view.GetItemColumnDataAsDouble(
                id,
                HierarchyFrameDataView.columnWarningCount)
        };

        if (view.HasItemChildren(id))
        {
            var children = new List<int>();

            view.GetItemChildren(id, children);

            foreach (int child in children)
                sample.children.Add(ReadSample(view, child));
        }

        return sample;
    }

    private static string GetCaptureFolder()
    {
        string projectRoot =
            Path.GetDirectoryName(Application.dataPath);

        return Path.Combine(
            projectRoot,
            "ProfilerCaptures");
    }
}