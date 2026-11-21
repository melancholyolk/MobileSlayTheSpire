using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Excel lives outside Assets. Poll on the editor main thread and only import after the subprocess succeeds.
[InitializeOnLoad]
public static class TableBuildPipeline
{
    static string last, candidate;
    static double next;
    static Process process;
    static readonly object OutputLock=new object();
    static string output="";
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static TableBuildPipeline()
    {
        EditorApplication.update+=Update;
        AssemblyReloadEvents.beforeAssemblyReload+=()=> { if(process!=null) { process.WaitForExit(); process.Dispose(); process=null; } };
    }
    static string Fingerprint()
    {
        var folder=Path.Combine(Root,"GameData/Excel");
        if(!Directory.Exists(folder)) return "missing";
        return string.Join("|",Directory.GetFiles(folder,"*",SearchOption.AllDirectories)
            .Where(p=>new[]{".xlsx",".csv",".json"}.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .Where(p=>!Path.GetFileName(p).StartsWith("~$")).OrderBy(p=>p)
            .Select(p=>p+":"+File.GetLastWriteTimeUtc(p).Ticks+":"+new FileInfo(p).Length));
    }
    static void Update()
    {
        if(process!=null)
        {
            if(!process.HasExited) return;
            process.WaitForExit(); int code=process.ExitCode;
            process.Dispose(); process=null;
            string result; lock(OutputLock) result=output;
            if(code==0) { UnityEngine.Debug.Log(result); AssetDatabase.Refresh(); }
            else UnityEngine.Debug.LogError("Excel build failed; last good assets retained.\n"+result);
        }
        if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        next=EditorApplication.timeSinceStartup+1;
        try
        {
            var current=Fingerprint();
            if(current==candidate && current!=last) { last=current; StartBuild(); }
            candidate=current;
        }
        catch(IOException) { /* Excel is replacing its saved file; retry next tick. */ }
    }
    [MenuItem("Tools/Game Tables/Build Excel Tables")]
    public static void StartBuild()
    {
        if(process!=null) return;
        lock(OutputLock) output="";
        var info=new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(Root,"Tools/Tables/run.ps1")+"\"")
        { WorkingDirectory=Root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        process=new Process { StartInfo=info };
        process.OutputDataReceived+=(s,e)=> { if(e.Data!=null) lock(OutputLock) output+=e.Data+"\n"; };
        process.ErrorDataReceived+=(s,e)=> { if(e.Data!=null) lock(OutputLock) output+=e.Data+"\n"; };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
    }
    public static void BuildBlocking()
    {
        StartBuild();
        process.WaitForExit();
        int code=process.ExitCode; process.Dispose(); process=null;
        if(code!=0) throw new BuildFailedException("Excel build failed:\n"+output);
        AssetDatabase.Refresh();
    }
}
public sealed class TableBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder=>-1000;
    public void OnPreprocessBuild(BuildReport report) => TableBuildPipeline.BuildBlocking();
}
