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

        /// <summary>在指定直线/二次/三次Bezier段内插入锚点；曲线使用de Casteljau精确拆分而非采样折线。</summary>
        public static FGEPath2D SplitSegment(FGEPath2D path,int commandIndex,double t)
        {
            if(path==null)throw new ArgumentNullException("path");
            FGEValidation.Finite(t,"t");
            if(t<=0||t>=1)throw new ArgumentOutOfRangeException("t");
            if(commandIndex<=0||commandIndex>=path.Commands.Count)throw new ArgumentOutOfRangeException("commandIndex");
            var all=new List<FGEPathCommand>(path.Commands);
            FGEPathCommand segment=all[commandIndex];
            if(segment.Verb!=FGEPathVerb.Line&&segment.Verb!=FGEPathVerb.Quadratic&&
                segment.Verb!=FGEPathVerb.Cubic)throw new ArgumentException("geometry.segment.unsupported");
            FGEPoint2D start=EndBefore(path.Commands,commandIndex);
            FGEPoint2D a=segment.End;
            if(segment.Verb==FGEPathVerb.Line)
            {
                all.Insert(commandIndex,FGEPathCommand.Line(Lerp(start,a,t)));
            }
            else if(segment.Verb==FGEPathVerb.Quadratic)
            {
                FGEPoint2D p=Lerp(start,segment.Control1,t);
                FGEPoint2D q=Lerp(segment.Control1,a,t);
                FGEPoint2D middle=Lerp(p,q,t);
                all[commandIndex]=FGEPathCommand.Quadratic(q,a);
                all.Insert(commandIndex,FGEPathCommand.Quadratic(p,middle));
            }
            else
            {
                FGEPoint2D a1=Lerp(start,segment.Control1,t);
                FGEPoint2D a2=Lerp(segment.Control1,segment.Control2,t);
                FGEPoint2D a3=Lerp(segment.Control2,a,t);
                FGEPoint2D b1=Lerp(a1,a2,t),b2=Lerp(a2,a3,t);
                FGEPoint2D middle=Lerp(b1,b2,t);
                all[commandIndex]=FGEPathCommand.Cubic(b2,a3,a);
                all.Insert(commandIndex,FGEPathCommand.Cubic(a1,b1,middle));
            }
            return new FGEPath2D(all);
        }
        private static FGEPoint2D Lerp(FGEPoint2D a,FGEPoint2D b,double t)
        {return new FGEPoint2D(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);}
        private static FGEPoint2D EndBefore(IReadOnlyList<FGEPathCommand> commands,int index)
        {
            for(int i=index-1;i>=0;i--)
            {
                if(commands[i].Verb==FGEPathVerb.Close)throw new ArgumentException("geometry.contour.closed");
                if(commands[i].Verb!=FGEPathVerb.Close)return commands[i].End;
            }
            throw new ArgumentException("geometry.path.missing_move");
        }
        /// <summary>删除中间的线段端点或独立轮廓；不隐式改变其余子路径。</summary>
        public static FGEPath2D DeleteAnchor(FGEPath2D path,int commandIndex)
        {
            if(path==null)throw new ArgumentNullException("path");
            if(commandIndex<=0||commandIndex>=path.Commands.Count)throw new ArgumentOutOfRangeException("commandIndex");
            var all=new List<FGEPathCommand>(path.Commands);
            FGEPathCommand selected=all[commandIndex];
            if(selected.Verb!=FGEPathVerb.Line)throw new ArgumentException("geometry.delete_requires_line_anchor");
            int start=commandIndex;
            while(start>0&&all[start].Verb!=FGEPathVerb.Move)start--;
            int count=0;
            for(int i=start+1;i<all.Count&&all[i].Verb!=FGEPathVerb.Close&&all[i].Verb!=FGEPathVerb.Move;i++)count++;
            if(count<=1)throw new ArgumentException("geometry.contour.minimum_segments");
            all.RemoveAt(commandIndex);
            return new FGEPath2D(all);
        }
        /// <summary>仅转换单一段，保留原端点，Bezier初始柄按直线三等分。</summary>
        public static FGEPath2D ConvertSegment(FGEPath2D path,int index,FGEPathVerb to)
        {
            if(path==null)throw new ArgumentNullException("path");
            if(index<=0||index>=path.Commands.Count)throw new ArgumentOutOfRangeException("index");
            if(to!=FGEPathVerb.Line&&to!=FGEPathVerb.Quadratic&&to!=FGEPathVerb.Cubic)
                throw new ArgumentException("geometry.segment.conversion");
            var all=new List<FGEPathCommand>(path.Commands);
            FGEPathCommand old=all[index];
            if(old.Verb!=FGEPathVerb.Line&&old.Verb!=FGEPathVerb.Quadratic&&
                old.Verb!=FGEPathVerb.Cubic)throw new ArgumentException("geometry.segment.unsupported");
            FGEPoint2D start=EndBefore(path.Commands,index),end=old.End;
            if(to==FGEPathVerb.Line)all[index]=FGEPathCommand.Line(end);
            else if(to==FGEPathVerb.Quadratic)
                all[index]=FGEPathCommand.Quadratic(
                    old.Verb==FGEPathVerb.Quadratic?old.Control1:
                    Lerp(start,end,0.5),end);
            else all[index]=FGEPathCommand.Cubic(
                old.Verb==FGEPathVerb.Cubic?old.Control1:Lerp(start,end,1.0/3),
                old.Verb==FGEPathVerb.Cubic?old.Control2:Lerp(start,end,2.0/3),end);
            return new FGEPath2D(all);
        }
        /// <summary>精确编辑Arc参数；起点Move自动重算，避免Path几何断裂。</summary>
        public static FGEPath2D UpdateArc(FGEPath2D path,int arcCommandIndex,FGEArc2D arc)
        {
            if(path==null)throw new ArgumentNullException("path");
            if(arcCommandIndex<=0||arcCommandIndex>=path.Commands.Count||
                path.Commands[arcCommandIndex].Verb!=FGEPathVerb.Arc||
                path.Commands[arcCommandIndex-1].Verb!=FGEPathVerb.Move)
                throw new ArgumentException("geometry.arc.segment_not_standalone");
            var all=new List<FGEPathCommand>(path.Commands);
            all[arcCommandIndex-1]=FGEPathCommand.Move(arc.PointAt(0));
            all[arcCommandIndex]=FGEPathCommand.ArcTo(arc);
            return new FGEPath2D(all);
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
