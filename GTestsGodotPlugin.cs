#if TOOLS

using Godot;

namespace GTestsGodot;

[Tool]
public partial class GTestsGodotPlugin : EditorPlugin
{
	Control? _dock;

	public override void _EnterTree()
	{
		string path = "addons/GTestsGodot/Scenes/TestRunnerDock.tscn";
		
		PackedScene testRunnerDockScene = GD.Load<PackedScene>(path);
		_dock = testRunnerDockScene.Instantiate<Control>();
            
		AddControlToDock(DockSlot.RightBl, _dock);
	}

	public override void _ExitTree()
	{
		if (_dock == null)
		{
			return;
		}
		
		RemoveControlFromDocks(_dock);
		
		_dock.Free();
	}
}

#endif
