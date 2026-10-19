using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Geometry
{
    /// <summary>路径命令的精确控制点编辑；仅返回新不可变路径，不依赖UI或工程模型。</summary>
    public enum FGEPathHandleKind { Anchor=1, Control1=2, Control2=3 }

    public static class FGEPathEditing
    {
        public static FGEPoint2D HandlePoint(FGEPath2D path,int commandIndex,FGEPathHandleKind kind)
        {
            if(path==null)throw new ArgumentNullException("path");
            if(commandIndex<0||commandIndex>=path.Commands.Count)throw new ArgumentOutOfRangeException("commandIndex");
            FGEPathCommand cmd=path.Commands[commandIndex];
            if(kind==FGEPathHandleKind.Anchor && cmd.Verb!=FGEPathVerb.Close && cmd.Verb!=FGEPathVerb.Arc)
                return cmd.End;
            if(kind==FGEPathHandleKind.Control1 &&
                (cmd.Verb==FGEPathVerb.Quadratic||cmd.Verb==FGEPathVerb.Cubic))
                return cmd.Control1;
            if(kind==FGEPathHandleKind.Control2 && cmd.Verb==FGEPathVerb.Cubic)
                return cmd.Control2;
            throw new ArgumentException("geometry.path.handle_not_supported");
        }

        public static FGEPath2D MoveHandle(FGEPath2D path,int commandIndex,FGEPathHandleKind kind,FGEPoint2D point)
        {
            HandlePoint(path,commandIndex,kind); // 验证该命令是否具有此控制点
            var commands=new List<FGEPathCommand>(path.Commands);
            // Arc 的起点由圆心/半径/角度约束，不能单独拖 Move 破坏连续性。
            if(kind==FGEPathHandleKind.Anchor&&
                commands[commandIndex].Verb==FGEPathVerb.Move&&
                commandIndex+1<commands.Count&&
                commands[commandIndex+1].Verb==FGEPathVerb.Arc)
                throw new ArgumentException("geometry.arc_start_is_derived");
            FGEPathCommand old=commands[commandIndex];
            switch(old.Verb)
            {
                case FGEPathVerb.Move:
                    commands[commandIndex]=FGEPathCommand.Move(point);
                    break;
                case FGEPathVerb.Line:
                    commands[commandIndex]=FGEPathCommand.Line(point);
                    break;
                case FGEPathVerb.Quadratic:
                    commands[commandIndex]=kind==FGEPathHandleKind.Control1?
                        FGEPathCommand.Quadratic(point,old.End):
                        FGEPathCommand.Quadratic(old.Control1,point);
                    break;
                case FGEPathVerb.Cubic:
                    commands[commandIndex]=kind==FGEPathHandleKind.Control1?
                        FGEPathCommand.Cubic(point,old.Control2,old.End):
                        kind==FGEPathHandleKind.Control2?
                        FGEPathCommand.Cubic(old.Control1,point,old.End):
                        FGEPathCommand.Cubic(old.Control1,old.Control2,point);
                    break;
                default:throw new ArgumentException("geometry.path.handle_not_supported");
            }
            return new FGEPath2D(commands);
        }
    }
}
