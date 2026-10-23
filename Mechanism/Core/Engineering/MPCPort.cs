using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Core
{
    public enum MPCPortDirection {Input=1,Output=2,InOut=3}
    public enum MPCPortKind {Data=1,Control=2,Event=3,Command=4,State=5,Stream=6,Resource=7,Visual=8}
    public enum MPCPortMultiplicity {One=1,Many=2}
    public sealed class MTUContractReference : IEquatable<MTUContractReference>
    {
        public MTUContractReference(string unitId="",string coordinateFrameId="",string timeBaseId="",MRIObjectAddress calibration=null)
        {UnitId=unitId??"";CoordinateFrameId=coordinateFrameId??"";TimeBaseId=timeBaseId??"";Calibration=calibration;}
        public string UnitId {get;private set;}
        public string CoordinateFrameId {get;private set;}
        public string TimeBaseId {get;private set;}
        public MRIObjectAddress Calibration {get;private set;}
        public bool Equals(MTUContractReference other){return other!=null&&UnitId==other.UnitId&&CoordinateFrameId==other.CoordinateFrameId&&TimeBaseId==other.TimeBaseId&&object.Equals(Calibration,other.Calibration);}
        public override bool Equals(object value){return Equals(value as MTUContractReference);}
        public override int GetHashCode(){return UnitId.GetHashCode()^CoordinateFrameId.GetHashCode()^TimeBaseId.GetHashCode();}
    }
    public sealed class MPCPort
    {
        internal MPCPort(Guid id,Guid ownerId,string memberId,MPCPortDirection direction,string typeId,MPCPortKind kind,MPCPortMultiplicity multiplicity,MTUContractReference contract,bool isPublic,bool required=false)
        {
            if(id==Guid.Empty||ownerId==Guid.Empty)throw new ArgumentException("Port identity required");
            if(!Enum.IsDefined(typeof(MPCPortDirection),direction)||!Enum.IsDefined(typeof(MPCPortKind),kind)||!Enum.IsDefined(typeof(MPCPortMultiplicity),multiplicity))throw new ArgumentException("Invalid port enum");
            Id=id;OwnerId=ownerId;MemberId=MEMNode.NormalizeName(memberId);TypeId=MEMNode.NormalizeName(typeId);Direction=direction;Kind=kind;Multiplicity=multiplicity;Contract=contract??new MTUContractReference();IsPublic=isPublic;Required=required;
        }
        public Guid Id {get;private set;} public Guid OwnerId {get;private set;}
        public string MemberId {get;private set;} public string TypeId {get;private set;}
        public MPCPortDirection Direction {get;private set;} public MPCPortKind Kind {get;private set;}
        public MPCPortMultiplicity Multiplicity {get;private set;} public MTUContractReference Contract {get;private set;}
        public bool IsPublic {get;private set;} public bool Required {get;private set;}
    }
    public sealed class MPCEndpoint : IEquatable<MPCEndpoint>
    {
        public MPCEndpoint(MRIObjectAddress address,Guid portId){if(address==null||portId==Guid.Empty)throw new ArgumentException("Endpoint identity required");Address=address;PortId=portId;}
        public MRIObjectAddress Address {get;private set;} public Guid PortId {get;private set;}
        public bool Equals(MPCEndpoint other){return other!=null&&Address.Equals(other.Address)&&PortId==other.PortId;}
        public override bool Equals(object value){return Equals(value as MPCEndpoint);}
        public override int GetHashCode(){return Address.GetHashCode()*397^PortId.GetHashCode();}
    }
    /// <summary>仅控制Visual端口的画线方式；其他MPC连接保持原连接语义。</summary>
    public enum MPCVisualRouteKind { Straight=1, Orthogonal=2, Manual=3 }

    /// <summary>折点相对两端线性插值位置的偏移，端点移动时折点连续跟随。</summary>
    public sealed class MPCVisualWaypoint
    {
        public MPCVisualWaypoint(double fraction,double offsetX,double offsetY)
        {
            FGEValidation.Finite(fraction,"fraction");
            FGEValidation.Finite(offsetX,"offsetX");
            FGEValidation.Finite(offsetY,"offsetY");
            if(fraction<=0||fraction>=1)
                throw new ArgumentOutOfRangeException("fraction");
            Fraction=fraction;OffsetX=offsetX;OffsetY=offsetY;
        }
        public double Fraction{get;private set;}
        public double OffsetX{get;private set;}
        public double OffsetY{get;private set;}
    }

    public sealed class MPCConnection
    {
        private IReadOnlyList<MPCVisualWaypoint> _visualWaypoints=
            new ReadOnlyCollection<MPCVisualWaypoint>(new List<MPCVisualWaypoint>());
        internal MPCConnection(Guid id,Guid ownerId,MPCEndpoint source,MPCEndpoint target)
        {
            if(id==Guid.Empty||ownerId==Guid.Empty||source==null||target==null)
                throw new ArgumentException("Connection identity/endpoints required");
            Id=id;OwnerId=ownerId;Source=source;Target=target;
            VisualRouteKind=MPCVisualRouteKind.Straight;
        }
        public Guid Id{get;private set;}
        public Guid OwnerId{get;private set;}
        public MPCEndpoint Source{get;private set;}
        public MPCEndpoint Target{get;private set;}
        public MPCVisualRouteKind VisualRouteKind{get;private set;}
        public IReadOnlyList<MPCVisualWaypoint> VisualWaypoints{get{return _visualWaypoints;}}
        /// <summary>仅通过MTC事务修改端点，保留原连接身份。</summary>
        internal void SetEndpoints(MPCEndpoint source,MPCEndpoint target)
        {
            if(source==null||target==null)throw new ArgumentNullException(
                source==null?"source":"target");
            Source=source;Target=target;
        }
        internal void SetVisualRoute(MPCVisualRouteKind kind,
            IEnumerable<MPCVisualWaypoint> waypoints)
        {
            if(!Enum.IsDefined(typeof(MPCVisualRouteKind),kind))
                throw new ArgumentOutOfRangeException("kind");
            if(waypoints==null)throw new ArgumentNullException("waypoints");
            var all=new List<MPCVisualWaypoint>(waypoints);
            if(all.Count>64||all.Exists(x=>x==null)||
                (kind!=MPCVisualRouteKind.Manual&&all.Count!=0))
                throw new ArgumentException("visual.route.invalid_waypoints");
            VisualRouteKind=kind;
            _visualWaypoints=new ReadOnlyCollection<MPCVisualWaypoint>(all);
        }
        internal MPCConnection CopyRouteFrom(MPCConnection source)
        {
            if(source==null)throw new ArgumentNullException("source");
            SetVisualRoute(source.VisualRouteKind,source.VisualWaypoints);
            return this;
        }
    }
    public enum MRCPublicMappingKind {Property=1,Port=2}
    public sealed class MRCPublicMapping
    {
        internal MRCPublicMapping(Guid owner,string member,MRIObjectAddress target,string targetMember)
        {OwnerId=owner;MemberId=MEMNode.NormalizeName(member);Kind=MRCPublicMappingKind.Property;TargetAddress=target??throw new ArgumentNullException("target");TargetMemberId=MEMNode.NormalizeName(targetMember);}
        internal MRCPublicMapping(Guid owner,string member,Guid publicPort,MPCEndpoint target)
        {OwnerId=owner;MemberId=MEMNode.NormalizeName(member);Kind=MRCPublicMappingKind.Port;PublicPortId=publicPort;TargetAddress=target.Address;TargetPortId=target.PortId;}
        public Guid OwnerId {get;private set;} public string MemberId {get;private set;}
        public MRCPublicMappingKind Kind {get;private set;}
        public MRIObjectAddress TargetAddress {get;private set;} public string TargetMemberId {get;private set;}
        public Guid? PublicPortId {get;private set;} public Guid? TargetPortId {get;private set;}
    }
}
