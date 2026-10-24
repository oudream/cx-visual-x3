using CxVisual.Foundation.Data.Messages;
using System;
using System.Collections.Generic;
using CxVisual.Foundation.Core;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Messages;
using CxVisual.Foundation.Data.Commands;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class InteractionStageOneTests
    {
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("BUS.P0-1 "+message);}
        private static void Reject<T>(Action action) where T:Exception
        {
            bool result=false;try{action();}catch(T){result=true;}
            Check(result,"Expected "+typeof(T).Name);
        }
        public static void Run()
        {
            long epoch=new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc).Ticks/TimeSpan.TicksPerMillisecond;
            long time=epoch+12345L;
            long first;
            using(FOIInteractionIdGenerator id=new FOIInteractionIdGenerator(947,()=>time))
            {
                first=id.NextId();Check(id.NextId()>first,"id monotonic");
                Reject<InvalidOperationException>(()=>new FOIInteractionIdGenerator(947,()=>time));
                --time;Reject<InvalidOperationException>(()=>id.NextId());++time;
            }
            using(FOIInteractionIdGenerator again=new FOIInteractionIdGenerator(947,()=>time))
                Check(again.NextId()>first,"worker restart replay");
            Guid run=Guid.NewGuid();
            string path="/machine/1201/app/XRay/module/Camera/message/Captured";
            FOIInteractionEndpointAddress address=new FOIInteractionEndpointAddress(path,first,run,1);
            Reject<ArgumentException>(()=>FOIInteractionEndpointAddress.NormalizePath("/machine/1201/app/XRay/module/Camera/**/Captured"));
            Reject<ArgumentException>(()=>FOIInteractionEndpointAddress.NormalizePath("/machine/1201/app/XRay/module/Camera/command"));
            using(MRIEndpointRegistry registry=new MRIEndpointRegistry(run))
            {
                MRIEndpointRegistration token=registry.Register(address);
                Check(object.ReferenceEquals(registry.Resolve(path),registry.Resolve(first)),"dual address");
                Reject<InvalidOperationException>(()=>registry.Register(address));
                Reject<InvalidOperationException>(()=>registry.Register(
                    new FOIInteractionEndpointAddress(path,first+1,Guid.NewGuid(),1)));
                token.Dispose();Check(registry.Resolve(first)==null,"unregistered");
                FOIInteractionEndpointAddress newer=new FOIInteractionEndpointAddress(path,first+1,run,2);
                using(registry.Register(newer))
                {
                    token.Dispose();
                    Check(object.ReferenceEquals(registry.Resolve(path),newer),"old token after new registration");
                }
                Check(registry.Snapshot().Length==0,"disposed handler");
            }
            MNMMessageTypeDefinition type=new MNMMessageTypeDefinition("Camera.Captured",1,typeof(string));
            type.Validate("image1");Reject<ArgumentException>(()=>type.Validate(123));
            FDCCommandParameterDefinition pos=new FDCCommandParameterDefinition("Position",typeof(double),true);
            FDCCommandParameterDefinition vel=new FDCCommandParameterDefinition("Velocity",typeof(double),false,true,20.0);
            FDCCommandParameterDefinition enable=new FDCCommandParameterDefinition("Enable",typeof(bool),false,true,true);
            FDCCommandDefinition cmd=new FDCCommandDefinition(1001,"MoveAbs","绝对移动",
                new[]{pos,vel,enable},
                new[]{new FDCCommandParameterDefinition("ActualPosition",typeof(double),true)});
            IReadOnlyDictionary<string,object> inputs=cmd.BindInputs(
                new Dictionary<string,object>{{"Position",0.0},{"Enable",false}});
            Check((double)inputs["Position"]==0.0 && !(bool)inputs["Enable"] &&
                (double)inputs["Velocity"]==20.0,"defaults and explicit zero/false");
            Reject<ArgumentException>(()=>cmd.BindInputs(new Dictionary<string,object>()));
            Reject<ArgumentException>(()=>cmd.BindInputs(new Dictionary<string,object>{{"Position","wrong"}}));
            Reject<ArgumentException>(()=>cmd.BindInputs(new Dictionary<string,object>{{"Position",1.0},{"Bad",2}}));
            Reject<ArgumentException>(()=>cmd.ValidateOutputs(new Dictionary<string,object>{{"ActualPosition","wrong"}}));
            FDCCommandDefinitionCatalog catalog=new FDCCommandDefinitionCatalog(new[]{cmd});
            Check(object.ReferenceEquals(catalog.Find(1001),catalog.Find("MoveAbs")),"definition catalog");
            Reject<ArgumentException>(()=>new FDCCommandDefinitionCatalog(new[]{cmd,cmd}));
            InteractionDefinitionsTests.Run();
            Console.WriteLine("PASS BUS.P0-1 ID / endpoint / schema / catalog");
        }
    }
}
