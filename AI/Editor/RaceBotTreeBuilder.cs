using System.Reflection;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace SprunkiRacing.Editor
{
    public static class RaceBotTreeBuilder
    {
        private const string AssetPath = "Assets/Game/AI/RaceBot.asset";

        [MenuItem("Sprunki/Build Race Bot Tree")]
        public static void Build()
        {
            ExternalBehaviorTree tree = AssetDatabase.LoadAssetAtPath<ExternalBehaviorTree>(AssetPath);
            if (tree == null)
            {
                tree = ScriptableObject.CreateInstance<ExternalBehaviorTree>();
                AssetDatabase.CreateAsset(tree, AssetPath);
            }

            BehaviorSource source = new BehaviorSource();
            MethodInfo initialize = typeof(BehaviorSource).GetMethod("Initialize");
            if (initialize != null)
                initialize.Invoke(source, new object[] { tree });

            RaceBotTree.Populate(source);
            MethodInfo setSource = typeof(ExternalBehavior).GetMethod("SetBehaviorSource");
            if (setSource != null)
                setSource.Invoke(tree, new object[] { source });
            TrySerialize(source);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            Debug.Log("Race bot tree saved to " + AssetPath);
        }

        private static void TrySerialize(BehaviorSource source)
        {
            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type binary = assembly.GetType("BehaviorDesigner.Editor.BinarySerialization");
                System.Type json = assembly.GetType("BehaviorDesigner.Editor.JSONSerialization");
                if (InvokeSave(binary, source) || InvokeSave(json, source))
                    return;
            }
        }

        private static bool InvokeSave(System.Type type, BehaviorSource source)
        {
            if (type == null)
                return false;

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "Save")
                    continue;

                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(BehaviorSource))
                {
                    methods[i].Invoke(null, new object[] { source });
                    return true;
                }
            }

            return false;
        }
    }
}
