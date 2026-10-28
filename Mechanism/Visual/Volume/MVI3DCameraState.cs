using System;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>单个三维视图会话的唯一相机状态：弧度、归一化场景坐标、版本。</summary>
    public sealed class MVI3DCameraState
    {
        private const double LimitPitch=1.47;
        private double _yaw=.62, _pitch=.36, _distance=2.9;
        private double _x,_y,_z;
        private long _revision;
        public event EventHandler Changed;
        public double YawRadians { get {return _yaw;} }
        public double PitchRadians { get {return _pitch;} }
        public double Distance {get {return _distance;} }
        public double TargetX {get {return _x;} }
        public double TargetY {get {return _y;} }
        public double TargetZ {get {return _z;} }
        public long Revision {get {return _revision;} }

        public void SetPose(double yaw,double pitch,double distance,double x,double y,double z)
        {
            if(!Finite(yaw)||!Finite(pitch)||!Finite(distance)||
               !Finite(x)||!Finite(y)||!Finite(z))
                throw new ArgumentOutOfRangeException("yaw","相机姿态必须全部为有限数值。");
            if(distance<=0)throw new ArgumentOutOfRangeException("distance");
            pitch=Math.Max(-LimitPitch,Math.Min(LimitPitch,pitch));
            distance=Math.Max(.45,Math.Min(16,distance));
            if(yaw==_yaw&&pitch==_pitch&&distance==_distance&&
               x==_x&&y==_y&&z==_z)return;
            _yaw=yaw;_pitch=pitch;_distance=distance;
            _x=x;_y=y;_z=z;
            _revision=checked(_revision+1);
            EventHandler callback=Changed;
            if(callback!=null)callback(this,EventArgs.Empty);
        }
        public void Reset()
        {
            SetPose(.62,.36,2.9,0,0,0);
        }
        /// <summary>弧度增量；由使用方处理DPI和手势到角度的转换。</summary>
        public void Orbit(double yawDelta,double pitchDelta)
        {
            SetPose(_yaw+yawDelta,_pitch+pitchDelta,_distance,_x,_y,_z);
        }
        public void Zoom(double wheelSteps)
        {
            if(!Finite(wheelSteps))throw new ArgumentOutOfRangeException("wheelSteps");
            if(wheelSteps==0)return;
            SetPose(_yaw,_pitch,_distance*Math.Pow(.87,wheelSteps),_x,_y,_z);
        }
        public void Pan(double dx,double dy,double viewportHeight)
        {
            if(!Finite(dx)||!Finite(dy)||!Finite(viewportHeight)||viewportHeight<=0)
                throw new ArgumentOutOfRangeException("viewportHeight");
            if(dx==0&&dy==0)return;
            double scale=2*_distance*Math.Tan(Math.PI/8)/viewportHeight;
            double rightX=Math.Cos(_yaw),rightZ=-Math.Sin(_yaw);
            double upX=-Math.Sin(_yaw)*Math.Sin(_pitch);
            double upY=Math.Cos(_pitch);
            double upZ=-Math.Cos(_yaw)*Math.Sin(_pitch);
            SetPose(_yaw,_pitch,_distance,
                _x+(-rightX*dx+upX*dy)*scale,
                _y+upY*dy*scale,
                _z+(-rightZ*dx+upZ*dy)*scale);
        }
        private static bool Finite(double value)
        {
            return !double.IsNaN(value)&&!double.IsInfinity(value);
        }
    }
}
