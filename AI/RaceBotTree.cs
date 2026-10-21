using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace SprunkiRacing
{
    public static class RaceBotTree
    {
        public static void Ensure(BehaviorTree tree)
        {
            if (tree == null)
                return;

            BehaviorSource source = tree.GetBehaviorSource();
            if (source == null || source.RootTask != null)
                return;

            Populate(source);
        }

        public static void Populate(BehaviorSource source)
        {
            EntryTask entry = new EntryTask();
            Task root = BuildRoot();
            AddChild(entry, root);
            source.EntryTask = entry;
            source.RootTask = root;
            source.HasSerialized = true;
        }

        public static Task BuildRoot()
        {
            Parallel parallel = new Parallel();
            AddChild(parallel, Forever(new ApplyDrive()));
            AddChild(parallel, Forever(BuildDecisions()));
            AddChild(parallel, Forever(BuildCombat()));
            return parallel;
        }

        private static Selector BuildDecisions()
        {
            Selector selector = new Selector();
            AddChild(selector, SequenceOf(new IsStuck(), new SetRecover()));
            AddChild(selector, SequenceOf(new ShouldSeekPickup(), new SetSeekPickup()));
            AddChild(selector, new SetRaceLine());
            return selector;
        }

        private static Sequence BuildCombat()
        {
            return SequenceOf(new HasCombatTarget(), new FireAtTarget());
        }

        private static Sequence SequenceOf(Task first, Task second)
        {
            Sequence sequence = new Sequence();
            AddChild(sequence, first);
            AddChild(sequence, second);
            return sequence;
        }

        private static Repeater Forever(Task child)
        {
            Repeater repeater = new Repeater();
            repeater.repeatForever = true;
            repeater.endOnFailure = false;
            AddChild(repeater, child);
            return repeater;
        }

        private static void AddChild(ParentTask parent, Task child)
        {
            Prepare(parent);
            Prepare(child);
            int index = parent.Children == null ? 0 : parent.Children.Count;
            parent.AddChild(child, index);
        }

        private static void Prepare(Task task)
        {
            if (task == null)
                return;

            FieldInfo field = typeof(Task).GetField("nodeData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.GetValue(task) != null)
                return;

            System.Type nodeType = typeof(BehaviorSource).Assembly.GetType("BehaviorDesigner.Runtime.NodeData");
            if (nodeType == null)
                return;

            field.SetValue(task, System.Activator.CreateInstance(nodeType));
        }
    }
}
