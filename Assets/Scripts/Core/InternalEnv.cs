﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if TensorFlow
using TensorFlow;
#endif

public class InternalEnv : BaseEnv
{
    public TextAsset graphModel;
    private string graphScope;
    private bool loaded = false;

#if TensorFlow
    TFGraph graph;
    TFSession session;
#endif

    protected override bool birdFly { get { return false; } }

    public override void Init()
    {
        base.Init();
        if (graphModel == null)
        {
            Debug.LogError("not found graph asset!");
            loaded = false;
        }
        else
        {
#if TensorFlow
            graph = new TFGraph();
            graph.Import(graphModel.bytes);
            session = new TFSession(graph);
#endif
            loaded = true;
        }
    }

    public override void OnTick()
    {
        base.OnTick();
        if (loaded)
        {
            int[] state = GetCurrentState();
            if (last_state != null)
            {
                UpdateState(last_state, state, last_r, last_action);
            }
            //do next loop
            BirdAction action = choose_action(state);
            GameMgr.S.RespondByDecision(action);
            last_r = 1;
            last_state = state;
            last_action = action;
        }
    }

    public override BirdAction choose_action(int[] state)
    {
#if TensorFlow
        var runner = session.GetRunner();
        float[,] fstate = new float[1, 3];
        for (int i = 0; i < state.Length; i++)
        {
            fstate[0, i] = state[i];
        }
        runner.AddInput(graph["state"][0], fstate);
        runner.Fetch(graph["pi/probweights"][0]);
        TFTensor[] networkOutput;
        try
        {
            networkOutput = runner.Run();
        }
        catch (TFException e)
        {
            string errorMessage = e.Message;
            try
            {
                errorMessage = $@"The tensorflow graph needs an input for {e.Message.Split(new string[] { "Node: " }, 0)[1].Split('=')[0]} of type {e.Message.Split(new string[] { "dtype=" }, 0)[1].Split(',')[0]}";
            }
            finally
            {
                throw new System.Exception(errorMessage + "  \n" + e.StackTrace);
            }
        }
        float[,] output = (float[,])networkOutput[0].GetValue();
        Debug.Log(string.Format("pi/probweights, fly:{0} pad:{1} ", output[0, 0], output[0, 1]));
        int rand = Random.Range(0, 100);
        return rand < (int)(output[0, 0] * 100) ? BirdAction.FLY : BirdAction.PAD;
#else
        throw new System.Exception("you should enable TensorFlow in playersettings symbols");
#endif
    }


    public override void UpdateState(int[] state, int[] state_, int rewd, BirdAction action)
    {
        //internal don't need to update env
    }

    public override void OnInspector()
    {
        base.OnInspector();
#if UNITY_EDITOR
        var serializedBrain = new SerializedObject(this);
        GUILayout.Label("Edit the Tensorflow graph parameters here");

        var tfGraphModel = serializedBrain.FindProperty("graphModel");
        serializedBrain.Update();
        EditorGUILayout.ObjectField(tfGraphModel);
        serializedBrain.ApplyModifiedProperties();

        if (graphModel == null)
        {
            EditorGUILayout.HelpBox("Please provide a tensorflow graph as a bytes file.", MessageType.Error);
        }
        // graphScope =
        //        EditorGUILayout.TextField(new GUIContent("Graph Scope",
        //            "If you set a scope while training your tensorflow model, " +
        //            "all your placeholder name will have a prefix. You must specify that prefix here."), graphScope);
#endif
    }

}