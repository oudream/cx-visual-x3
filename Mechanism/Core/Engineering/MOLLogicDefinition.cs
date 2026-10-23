using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CxVisual.Mechanism.Core
{
    // Only the Content owns this definition. Blocks are value-like records, not MEMNodes.
    public enum MOLControlKind { Action=1, Sequence=2, Branch=3, Loop=4, ForEach=5, Parallel=6, Invoke=7, Wait=8, Try=9, Exit=10 }
    public enum MOLBranchMode { If=1, Switch=2 }
    public enum MOLLoopMode { For=1, While=2, DoWhile=3 }
    public enum MOLExitKind { Break=1, Continue=2, Return=3, Throw=4 }
    public enum MOLExpressionSource { Constant=1, FlowInput=2, StepResult=3, Iteration=4 }
    public enum MOLComparison { Truthy=1, Equal=2, NotEqual=3, LessThan=4, LessOrEqual=5, GreaterThan=6, GreaterOrEqual=7 }
    // 不是另一套规则图：布尔组合仍属于MOLExpression本身的不可变值语义。
    public enum MOLLogicalOperator { Scalar=0, All=1, Any=2, Not=3 }

    public sealed class MOLExpression
    {
        public MOLExpression(MOLExpressionSource source, string name = null,
            MOLComparison comparison = MOLComparison.Truthy, string expected = null)
        {
            if (!Enum.IsDefined(typeof(MOLExpressionSource), source) ||
                !Enum.IsDefined(typeof(MOLComparison), comparison))
                throw new ArgumentException("Unknown expression operator");
            if ((source == MOLExpressionSource.FlowInput || source == MOLExpressionSource.StepResult) &&
                string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Expression source must have a name");
            if (comparison != MOLComparison.Truthy && expected == null)
                throw new ArgumentException("Comparison requires an expected value");
            Source = source; Name = name ?? ""; Comparison = comparison; Expected = expected;
            Operator=MOLLogicalOperator.Scalar;
            Terms=Array.AsReadOnly(new MOLExpression[0]);
        }
        private MOLExpression(MOLLogicalOperator operation,IEnumerable<MOLExpression> children)
        {
            MOLExpression[] values=(children??Enumerable.Empty<MOLExpression>()).ToArray();
            if((operation==MOLLogicalOperator.Not&&values.Length!=1)||
                ((operation==MOLLogicalOperator.All||operation==MOLLogicalOperator.Any)&&
                (values.Length<2||values.Length>16))||
                values.Any(x=>x==null))
                throw new ArgumentException("logic.expression_group_invalid");
            Operator=operation;Terms=Array.AsReadOnly(values);
            Source=MOLExpressionSource.Constant;Name="";
            Comparison=MOLComparison.Truthy;
            ValidateComplexity(this,0,new int[1]);
        }
        internal static void ValidateComplexity(MOLExpression expr,int depth,int[] count)
        {
            if(depth>16||++count[0]>128)
                throw new InvalidOperationException("logic.expression_complexity");
            foreach(MOLExpression term in expr.Terms)
                ValidateComplexity(term,depth+1,count);
        }
        public MOLLogicalOperator Operator {get;private set;}
        public IReadOnlyList<MOLExpression> Terms{get;private set;}
        public static MOLExpression All(params MOLExpression[] terms)
        {return new MOLExpression(MOLLogicalOperator.All,terms);}
        public static MOLExpression Any(params MOLExpression[] terms)
        {return new MOLExpression(MOLLogicalOperator.Any,terms);}
        public static MOLExpression Not(MOLExpression term)
        {return new MOLExpression(MOLLogicalOperator.Not,new[]{term});}
        public MOLExpressionSource Source { get; private set; }
        public string Name { get; private set; }
        public MOLComparison Comparison { get; private set; }
        public string Expected { get; private set; }
        public static MOLExpression InputEquals(string input, string value)
        { return new MOLExpression(MOLExpressionSource.FlowInput, input, MOLComparison.Equal, value); }
        public static MOLExpression InputTrue(string input)
        { return new MOLExpression(MOLExpressionSource.FlowInput, input); }
        public static MOLExpression Always(bool value)
        { return new MOLExpression(MOLExpressionSource.Constant, value ? "true" : "false"); }
    }

    /// <summary>跨结构化逻辑与状态机复用的纯标量表达式语义，无运行上下文/设备副作用。</summary>
    public static class MOLScalarExpression
    {
        public static bool Evaluate(MOLExpression expr,object actual)
        {
            if(expr==null)return true;
            if(expr.Comparison==MOLComparison.Truthy)
            {
                if(actual is bool)return (bool)actual;
                if(actual is string)
                {
                    bool value;
                    if(bool.TryParse((string)actual,out value))return value;
                }
                if(actual is int)return (int)actual!=0;
                throw new InvalidOperationException("moc.truth_type");
            }
            int order=Compare(actual,expr.Expected);
            switch(expr.Comparison)
            {
                case MOLComparison.Equal:return order==0;
                case MOLComparison.NotEqual:return order!=0;
                case MOLComparison.LessThan:return order<0;
                case MOLComparison.LessOrEqual:return order<=0;
                case MOLComparison.GreaterThan:return order>0;
                case MOLComparison.GreaterOrEqual:return order>=0;
                default:throw new InvalidOperationException("moc.expression_compare");
            }
        }
        /// <summary>
        /// 以实际标量类型为准：数值是数值，字符串是字符串；不在类型冲突时偷偷退化为字典序。
        /// 保留旧MOLExpression声明格式，供V1分支、V2 Guard和状态迁移共用。
        /// </summary>
        /// <summary>短路且有界；所有叶子沿用同一纯标量合同，外部只负责按声明取值。</summary>
        public static bool EvaluateTree(MOLExpression expr,Func<MOLExpression,object> resolve)
        {
            if(expr==null)return true;
            if(resolve==null)throw new ArgumentNullException("resolve");
            MOLExpression.ValidateComplexity(expr,0,new int[1]);
            Func<MOLExpression,bool> walk=null;
            walk=e=>{
                switch(e.Operator)
                {
                    case MOLLogicalOperator.Scalar:return Evaluate(e,resolve(e));
                    case MOLLogicalOperator.All:
                        foreach(MOLExpression t in e.Terms)if(!walk(t))return false;
                        return true;
                    case MOLLogicalOperator.Any:
                        foreach(MOLExpression t in e.Terms)if(walk(t))return true;
                        return false;
                    case MOLLogicalOperator.Not:return !walk(e.Terms[0]);
                    default:throw new InvalidOperationException("logic.expression_operator");
                }
            };
            return walk(expr);
        }
        public static int Compare(object actual,string expected)
        {
            if(actual==null||expected==null)
                throw new InvalidOperationException("moc.expression_null");
            TypeCode code=Type.GetTypeCode(actual.GetType());
            switch(code)
            {
                case TypeCode.Boolean:
                    bool value;
                    if(!bool.TryParse(expected,out value))
                        throw new InvalidOperationException("moc.expression_type");
                    return ((bool)actual).CompareTo(value);
                case TypeCode.String:
                    return string.CompareOrdinal((string)actual,expected);
                case TypeCode.Single:
                case TypeCode.Double:
                    double real=Convert.ToDouble(actual,CultureInfo.InvariantCulture),target;
                    if(double.IsNaN(real)||double.IsInfinity(real)||
                       !double.TryParse(expected,NumberStyles.Float,
                           CultureInfo.InvariantCulture,out target)||
                       double.IsNaN(target)||double.IsInfinity(target))
                        throw new InvalidOperationException("moc.expression_type");
                    return real.CompareTo(target);
                case TypeCode.SByte: case TypeCode.Byte:
                case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32:
                case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Decimal:
                    decimal number,expectedNumber;
                    try{number=Convert.ToDecimal(actual,CultureInfo.InvariantCulture);}
                    catch(Exception ex)when(ex is InvalidCastException||ex is OverflowException)
                    {throw new InvalidOperationException("moc.expression_type",ex);}
                    if(!decimal.TryParse(expected,NumberStyles.Float,
                        CultureInfo.InvariantCulture,out expectedNumber))
                        throw new InvalidOperationException("moc.expression_type");
                    return number.CompareTo(expectedNumber);
                default:throw new InvalidOperationException("moc.expression_type");
            }
        }
        public static bool EqualsScalar(object actual,string expected)
        {return Compare(actual,expected)==0;}
    }

    public sealed class MOLBranchCase
    {
        public MOLBranchCase(MOLExpression condition, MOLControlBlock body, string label = null)
        { Body = body ?? throw new ArgumentNullException("body"); Condition = condition; Label = label ?? ""; }
        public MOLExpression Condition { get; private set; } // null = else/default
        public MOLControlBlock Body { get; private set; }
        public string Label { get; private set; }
    }

    public sealed class MOLLoopDefinition
    {
        public MOLLoopDefinition(MOLLoopMode mode, int maxIterations, MOLControlBlock body,
            MOLExpression condition = null, string collectionInput = null)
        {
            if (!Enum.IsDefined(typeof(MOLLoopMode), mode)) throw new ArgumentException("Unknown loop kind");
            // R05动态区域只编译一次：迭代上限用于运行时安全预算，不再限制为静态展开的1024次。
            if (maxIterations < 1) throw new ArgumentOutOfRangeException("maxIterations");
            if (body == null) throw new ArgumentNullException("body");
            Mode = mode; MaxIterations = maxIterations; Body = body; Condition = condition;
            CollectionInput = collectionInput ?? "";
        }
        public MOLLoopMode Mode { get; private set; }
        public int MaxIterations { get; private set; }
        public MOLControlBlock Body { get; private set; }
        public MOLExpression Condition { get; private set; }
        public string CollectionInput { get; private set; }
    }

    public enum MOLEventSource { FlowInput=1, Channel=2 }
    /// <summary>事件只声明来源、关联与负载类型；订阅/投递/持久消费归MRK。</summary>
    public sealed class MOLEventContract
    {
        public MOLEventContract(string id,MOLEventSource source=MOLEventSource.FlowInput,
            string payloadTypeId=null,string correlationInput=null)
        {
            if(!Enum.IsDefined(typeof(MOLEventSource),source))
                throw new ArgumentException("logic.event_source");
            Id=Check(id,"logic.event_id");
            Source=source;
            PayloadTypeId=string.IsNullOrEmpty(payloadTypeId)?"":Check(payloadTypeId,"logic.event_type");
            CorrelationInput=string.IsNullOrEmpty(correlationInput)?"":
                Check(correlationInput,"logic.event_correlation");
        }
        private static string Check(string v,string code)
        {
            if(string.IsNullOrWhiteSpace(v)||v.Length>128||v.Any(char.IsControl))
                throw new ArgumentException(code);
            return v.Trim();
        }
        public string Id{get;private set;}
        public MOLEventSource Source{get;private set;}
        public string PayloadTypeId{get;private set;}
        public string CorrelationInput{get;private set;}
        public bool IsLegacyTask
        {get{return Source==MOLEventSource.FlowInput&&
            PayloadTypeId.Length==0&&CorrelationInput.Length==0;}}
    }
    /// <summary>工程等待声明：定时或有界外部事件；实际等待、取消由MRK执行。</summary>
    public sealed class MOLWaitDefinition
    {
        public MOLWaitDefinition(int durationMilliseconds)
        {
            if(durationMilliseconds<0||durationMilliseconds>600000)
                throw new ArgumentOutOfRangeException("durationMilliseconds");
            DurationMilliseconds=durationMilliseconds;
            EventInput="";Event=null;
        }
        /// <summary>保留旧Task输入合同。</summary>
        public MOLWaitDefinition(string eventInput,int timeoutMilliseconds)
            :this(new MOLEventContract(eventInput),timeoutMilliseconds){}
        public MOLWaitDefinition(MOLEventContract eventContract,int timeoutMilliseconds)
        {
            if(eventContract==null)throw new ArgumentNullException("eventContract");
            if(timeoutMilliseconds<1||timeoutMilliseconds>600000)
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            Event=eventContract;EventInput=eventContract.Id;
            DurationMilliseconds=timeoutMilliseconds;
        }
        public int DurationMilliseconds{get;private set;}
        public string EventInput{get;private set;}
        public MOLEventContract Event{get;private set;}
    }

    /// <summary>具名错误处理声明，错误码来自原Capability合同；不保存CLR异常类型。</summary>
    public sealed class MOLCatchClause
    {
        public MOLCatchClause(string errorCode,MOLControlBlock body)
        {
            if(string.IsNullOrWhiteSpace(errorCode)||errorCode.Length>128||
                errorCode.Any(char.IsControl)||errorCode=="*")
                throw new ArgumentException("logic.catch_invalid_code","errorCode");
            ErrorCode=errorCode.Trim();Body=body??throw new ArgumentNullException("body");
        }
        public string ErrorCode{get;private set;}
        public MOLControlBlock Body{get;private set;}
    }

    /// <summary>受保护的结构化控制区：定义归MOL，捕获/清理及实际执行归MRK。</summary>
    public sealed class MOLTryDefinition
    {
        public MOLTryDefinition(MOLControlBlock tryBody,
            MOLControlBlock catchBody=null,MOLControlBlock finallyBody=null,
            IEnumerable<MOLCatchClause> catches=null)
        {
            if(tryBody==null)throw new ArgumentNullException("tryBody");
            MOLCatchClause[] clauses=(catches??Enumerable.Empty<MOLCatchClause>()).ToArray();
            if(clauses.Length>32||clauses.Any(x=>x==null)||
               clauses.Select(x=>x.ErrorCode).Distinct(StringComparer.Ordinal).Count()!=clauses.Length)
                throw new ArgumentException("logic.catch_duplicate_or_budget");
            if(catchBody==null&&finallyBody==null&&clauses.Length==0)
                throw new ArgumentException("logic.try_requires_handler_or_finally");
            TryBody=tryBody;CatchBody=catchBody;FinallyBody=finallyBody;
            Catches=Array.AsReadOnly(clauses);
        }
        public MOLControlBlock TryBody{get;private set;}
        /// <summary>原无条件Catch，始终排在具名Catch之后；历史工程合同保持不变。</summary>
        public MOLControlBlock CatchBody{get;private set;}
        public MOLControlBlock FinallyBody{get;private set;}
        public IReadOnlyList<MOLCatchClause> Catches{get;private set;}
    }

    public sealed class MOLControlBlock
    {
        public MOLControlBlock(Guid id, MOLControlKind kind, string capabilityId = null,
            IEnumerable<MOLControlBlock> children = null, IEnumerable<MOLBranchCase> cases = null,
            MOLLoopDefinition loop = null, MOLExpression condition = null, string targetFlow = null,
            MOLExitKind exitKind = MOLExitKind.Return, MOLWaitDefinition wait = null,
            MOLTryDefinition tryDefinition = null,string errorCode=null)
        {
            if (id == Guid.Empty || !Enum.IsDefined(typeof(MOLControlKind), kind))
                throw new ArgumentException("Invalid logical block identity");
            Id = id; Kind = kind; CapabilityId = capabilityId ?? ""; Condition = condition;
            TargetFlow = targetFlow ?? ""; ExitKind = exitKind; Loop = loop; Wait = wait;
            TryRegion=tryDefinition;ErrorCode=errorCode??"";
            Children = Array.AsReadOnly((children ?? new MOLControlBlock[0]).ToArray());
            Cases = Array.AsReadOnly((cases ?? new MOLBranchCase[0]).ToArray());
        }
        public Guid Id { get; private set; }
        public MOLControlKind Kind { get; private set; }
        public string CapabilityId { get; private set; }
        public string TargetFlow { get; private set; }
        public MOLExpression Condition { get; private set; }
        public MOLLoopDefinition Loop { get; private set; }
        public MOLWaitDefinition Wait { get; private set; }
        public MOLTryDefinition TryRegion { get; private set; }
        public MOLExitKind ExitKind { get; private set; }
        /// <summary>Throw专用错误码，属于定义；实际异常与Trace归MRK。</summary>
        public string ErrorCode{get;private set;}
        public IReadOnlyList<MOLControlBlock> Children { get; private set; }
        public IReadOnlyList<MOLBranchCase> Cases { get; private set; }
        public static MOLControlBlock Delay(int durationMilliseconds)
        {return new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Wait,
            wait:new MOLWaitDefinition(durationMilliseconds));}
        public static MOLControlBlock AwaitEvent(string eventInput,int timeoutMilliseconds)
        {return new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Wait,
            wait:new MOLWaitDefinition(eventInput,timeoutMilliseconds));}
        public static MOLControlBlock AwaitEvent(MOLEventContract contract,int timeoutMilliseconds)
        {return new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Wait,
            wait:new MOLWaitDefinition(contract,timeoutMilliseconds));}
        public static MOLControlBlock TryCatchFinally(MOLControlBlock body,
            MOLControlBlock catchBody=null,MOLControlBlock finallyBody=null,
            IEnumerable<MOLCatchClause> catches=null)
        {return new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Try,
            tryDefinition:new MOLTryDefinition(body,catchBody,finallyBody,catches));}
        public static MOLControlBlock Action(string capability)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Action, capability); }
        public static MOLControlBlock Throw(string errorCode)
        {
            if(string.IsNullOrWhiteSpace(errorCode)||errorCode.Length>128||
                errorCode.Any(char.IsControl))
                throw new ArgumentException("logic.throw_bad_error_code","errorCode");
            return new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Exit,
                exitKind:MOLExitKind.Throw,errorCode:errorCode.Trim());
        }
        public static MOLControlBlock Sequence(params MOLControlBlock[] children)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Sequence, children: children); }
        public static MOLControlBlock Parallel(params MOLControlBlock[] children)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Parallel, children: children); }
        /// <summary>R02：按输入顺序选择第一个成立的Case；最后允许一个Else/Default。</summary>
        public static MOLControlBlock Branch(params MOLBranchCase[] cases)
        {
            if(cases==null)throw new ArgumentNullException("cases");
            // 子树尚未挂接到Loop前不能判断Break/Continue祖先作用域。
            // 工厂只校验本地Case规则，完整上下文交由MOLLogicDefinition校验。
            if(cases.Length==0)throw new InvalidOperationException("logic.no_cases");
            for(int i=0;i<cases.Length;i++)
            {
                if(cases[i]==null)throw new InvalidOperationException("logic.null_case");
                if(cases[i].Condition==null&&i!=cases.Length-1)
                    throw new InvalidOperationException("logic.default_must_be_last");
            }
            return new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Branch,cases:cases);
        }
        public static MOLControlBlock If(MOLExpression condition, MOLControlBlock yes, MOLControlBlock no = null)
        {
            if (condition == null) throw new ArgumentNullException("condition");
            var paths = new List<MOLBranchCase> { new MOLBranchCase(condition, yes, "True") };
            if (no != null) paths.Add(new MOLBranchCase(null, no, "Else"));
            return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Branch, cases: paths);
        }
        public static MOLControlBlock Repeat(MOLLoopMode mode, int count, MOLControlBlock body, MOLExpression condition = null)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Loop, loop: new MOLLoopDefinition(mode, count, body, condition)); }
        public static MOLControlBlock Each(string input, int maxItems, MOLControlBlock body)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.ForEach, loop:
            new MOLLoopDefinition(MOLLoopMode.For, maxItems, body, null, input)); }
    }

    public sealed class MOLLogicDefinition
    {
        public MOLLogicDefinition(MOLControlBlock root)
        { Root = root ?? throw new ArgumentNullException("root"); Validate(); }
        public MOLControlBlock Root { get; private set; }
        /// <summary>不可变替换一个逻辑块。保留其他块与关系的ID，结果整体重新校验。</summary>
        public MOLLogicDefinition ReplaceBlock(Guid id,MOLControlBlock replacement)
        {
            if(id==Guid.Empty||replacement==null)
                throw new ArgumentException("logic.edit.invalid_replacement");
            if(id!=replacement.Id)
                throw new InvalidOperationException("logic.edit.identity_changed");
            bool found=false;
            Func<MOLControlBlock,MOLControlBlock> walk=null;
            walk=b=>{
                if(b.Id==id){found=true;return replacement;}
                var children=b.Children.Select(walk).ToArray();
                var cases=b.Cases.Select(c=>new MOLBranchCase(
                    c.Condition,walk(c.Body),c.Label)).ToArray();
                MOLLoopDefinition loop=b.Loop==null?null:
                    new MOLLoopDefinition(b.Loop.Mode,b.Loop.MaxIterations,
                        walk(b.Loop.Body),b.Loop.Condition,b.Loop.CollectionInput);
                MOLTryDefinition region=b.TryRegion==null?null:
                    new MOLTryDefinition(walk(b.TryRegion.TryBody),
                        b.TryRegion.CatchBody==null?null:walk(b.TryRegion.CatchBody),
                        b.TryRegion.FinallyBody==null?null:walk(b.TryRegion.FinallyBody),
                        b.TryRegion.Catches.Select(c=>
                            new MOLCatchClause(c.ErrorCode,walk(c.Body))));
                return new MOLControlBlock(b.Id,b.Kind,b.CapabilityId,children,cases,
                    loop,b.Condition,b.TargetFlow,b.ExitKind,b.Wait,region,b.ErrorCode);
            };
            MOLControlBlock result=walk(Root);
            if(!found)throw new InvalidOperationException("logic.edit.block_not_found");
            return new MOLLogicDefinition(result);
        }
        public void Validate()
        {
            var ids = new HashSet<Guid>();
            int count = 0;
            Action<MOLControlBlock,int,int> walk = null;
            walk = (block, depth, loopDepth) =>
            {
                if (block == null || depth > 32 || ++count > 4096 || !ids.Add(block.Id))
                    throw new InvalidOperationException("logic.invalid_tree_or_identity");
                if (!Enum.IsDefined(typeof(MOLControlKind), block.Kind))
                    throw new InvalidOperationException("logic.unknown_kind");
                if (block.Kind != MOLControlKind.Sequence && block.Kind != MOLControlKind.Parallel &&
                    block.Children.Count != 0)
                    throw new InvalidOperationException("logic.unexpected_children");
                if (block.Kind != MOLControlKind.Branch && block.Cases.Count != 0)
                    throw new InvalidOperationException("logic.unexpected_cases");
                if(block.Kind!=MOLControlKind.Try&&block.TryRegion!=null)
                    throw new InvalidOperationException("logic.unexpected_try");
                if(block.Kind!=MOLControlKind.Wait&&block.Wait!=null)
                    throw new InvalidOperationException("logic.unexpected_wait");
                if (block.Kind != MOLControlKind.Loop && block.Kind != MOLControlKind.ForEach &&
                    block.Loop != null)
                    throw new InvalidOperationException("logic.unexpected_loop");
                switch (block.Kind)
                {
                    case MOLControlKind.Action:
                        if (string.IsNullOrWhiteSpace(block.CapabilityId)) throw new InvalidOperationException("logic.capability_missing");
                        break;
                    case MOLControlKind.Sequence:
                    case MOLControlKind.Parallel:
                        if (block.Children.Count == 0) throw new InvalidOperationException("logic.empty_container");
                        break;
                    case MOLControlKind.Branch:
                        if (block.Cases.Count == 0) throw new InvalidOperationException("logic.no_cases");
                        int defaults = 0;
                        for (int i = 0; i < block.Cases.Count; i++)
                        {
                            if (block.Cases[i] == null) throw new InvalidOperationException("logic.null_case");
                            if (block.Cases[i].Condition == null && (++defaults > 1 || i != block.Cases.Count - 1))
                                throw new InvalidOperationException("logic.default_must_be_last");
                            walk(block.Cases[i].Body, depth + 1,loopDepth);
                        }
                        break;
                    case MOLControlKind.Loop:
                    case MOLControlKind.ForEach:
                        if (block.Loop == null) throw new InvalidOperationException("logic.loop_missing");
                        if (block.Kind == MOLControlKind.Loop && block.Loop.Mode != MOLLoopMode.For &&
                            block.Loop.Condition == null)
                            throw new InvalidOperationException("logic.while_needs_condition");
                        if (block.Kind == MOLControlKind.ForEach && string.IsNullOrWhiteSpace(block.Loop.CollectionInput))
                            throw new InvalidOperationException("logic.collection_missing");
                        walk(block.Loop.Body, depth + 1,loopDepth+1);
                        break;
                    case MOLControlKind.Try:
                        if(block.TryRegion==null)
                            throw new InvalidOperationException("logic.try_missing");
                        walk(block.TryRegion.TryBody,depth+1,loopDepth);
                        if(block.TryRegion.CatchBody!=null)
                            walk(block.TryRegion.CatchBody,depth+1,loopDepth);
                        foreach(var clause in block.TryRegion.Catches)
                            walk(clause.Body,depth+1,loopDepth);
                        if(block.TryRegion.FinallyBody!=null)
                            walk(block.TryRegion.FinallyBody,depth+1,loopDepth);
                        break;
                    case MOLControlKind.Wait:
                        if(block.Wait==null)throw new InvalidOperationException("logic.wait_missing");
                        break;
                    case MOLControlKind.Invoke:
                        if (string.IsNullOrWhiteSpace(block.TargetFlow)) throw new InvalidOperationException("logic.invoke_missing");
                        break;
                    case MOLControlKind.Exit:
                        if(!Enum.IsDefined(typeof(MOLExitKind),block.ExitKind))
                            throw new InvalidOperationException("logic.exit_unknown");
                        if((block.ExitKind==MOLExitKind.Break||
                            block.ExitKind==MOLExitKind.Continue)&&loopDepth==0)
                            throw new InvalidOperationException("logic.exit_outside_loop");
                        if(block.ExitKind==MOLExitKind.Throw)
                        {
                            if(string.IsNullOrWhiteSpace(block.ErrorCode)||
                                block.ErrorCode.Length>128||block.ErrorCode.Any(char.IsControl))
                                throw new InvalidOperationException("logic.throw_bad_error_code");
                        }
                        else if(block.ErrorCode.Length!=0)
                            throw new InvalidOperationException("logic.unexpected_error_code");
                        break;
                }
                foreach (var child in block.Children) walk(child, depth + 1,loopDepth);
            };
            walk(Root, 0,0);
        }
    }

    // Xml payload is written into the existing optional Logic Aspect using the ordinary Session transaction.
    public static class MOLLogicCodec
    {
        public const string ProviderId = "cx.logic.structured";
        public const int SchemaVersion = 1;
        public const int ExtendedSchemaVersion=4;
        private static bool HasComposition(MOLControlBlock b)
        {
            Func<MOLExpression,bool> composite=e=>
                e!=null&&e.Operator!=MOLLogicalOperator.Scalar;
            return (b.Wait!=null&&b.Wait.Event!=null&&!b.Wait.Event.IsLegacyTask)||
                composite(b.Condition)||
                b.Cases.Any(c=>composite(c.Condition)||HasComposition(c.Body))||
                (b.Loop!=null&&(composite(b.Loop.Condition)||HasComposition(b.Loop.Body)))||
                b.Children.Any(HasComposition)||
                (b.TryRegion!=null&&(HasComposition(b.TryRegion.TryBody)||
                 (b.TryRegion.CatchBody!=null&&HasComposition(b.TryRegion.CatchBody))||
                 (b.TryRegion.FinallyBody!=null&&HasComposition(b.TryRegion.FinallyBody))||
                 b.TryRegion.Catches.Any(c=>HasComposition(c.Body))));
        }
        private static int Version(MOLLogicDefinition d)
        {return HasComposition(d.Root)?ExtendedSchemaVersion:SchemaVersion;}
        /// <summary>仅转换无端口、连线和策略的V2单一Action；不做有损Graph降级。</summary>
        public static void ConvertFromV2(MEMEngineeringSession session,MEMContent content)
        {
            if(session==null||content==null)throw new ArgumentNullException("session/content");
            int revision=session.Revision;
            MEMNodeSnapshot node=session.GetSnapshot().Find(content.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("logic.convert.foreign_content");
            MOLWiringDefinition wired=MOLWiringCodec.Get(node);
            if(wired==null)throw new InvalidOperationException("logic.convert.requires_v2");
            if(wired.Blocks.Count!=1||wired.Links.Count!=0||wired.Positions.Count!=0||
                wired.Groups.Count!=0||wired.Parameters.Count!=0||wired.Policies.Count!=0||
                wired.StateMachines.Count!=0||wired.FlowInputs.Count!=0||wired.FlowOutputs.Count!=0||
                wired.EntryBlockId!=wired.Blocks[0].Id||
                wired.Ports[wired.Blocks[0].Id].Count!=0)
                throw new InvalidOperationException("logic.convert.not_lossless");
            var action=wired.Blocks[0];
            if(action.Kind!=MOLControlKind.Action||action.Condition!=null||
                action.TargetFlow.Length!=0||action.ExitKind!=MOLExitKind.Return||
                action.Loop!=null||action.Wait!=null||action.TryRegion!=null||
                action.Children.Count!=0||action.Cases.Count!=0)
                throw new InvalidOperationException("logic.convert.not_lossless");
            var translated=new MOLLogicDefinition(action);
            byte[] payload=Serialize(translated);
            session.Edit("V2单动作无损转换到V1",revision,e=>{
                e.SetAspect(content,new MPMAspect(MPMAspectKind.Logic,
                    ProviderId,SchemaVersion,payload));return true;
            }).RequireValue();
        }
        public static void Set(MEMEngineeringSession session, MEMContent content, MOLLogicDefinition definition)
        {
            if (session == null || content == null || definition == null)
                throw new ArgumentNullException("session/content/definition");
            session.SetAspect(content, new MPMAspect(MPMAspectKind.Logic, ProviderId,
                Version(definition), Serialize(definition)));
        }
        public static MOLLogicDefinition Get(MEMNodeSnapshot content)
        {
            if (content == null) throw new ArgumentNullException("content");
            var aspect = content.Aspects.SingleOrDefault(a =>
                a.Kind == MPMAspectKind.Logic && a.ProviderId == ProviderId);
            if (aspect == null) return null;
            if(aspect.SchemaVersion==MOLWiringCodec.SchemaVersion||
               aspect.SchemaVersion==MOLWiringCodec.ExtendedSchemaVersion)return null;
            if((aspect.SchemaVersion!=SchemaVersion&&aspect.SchemaVersion!=ExtendedSchemaVersion)||
               aspect.RequiresRemap)
                throw new InvalidDataException("logic.unsupported_schema_or_remap");
            return Deserialize(aspect.Payload,aspect.SchemaVersion);
        }
        /// <summary>结构化V1使用原MEM事务编辑；绝不静默覆盖V2或坏Schema。</summary>
        public static void Edit(MEMEngineeringSession session,MEMContent content,
            Func<MOLLogicDefinition,MOLLogicDefinition> change)
        {
            if(session==null||content==null||change==null)
                throw new ArgumentNullException("session/content/change");
            int revision=session.Revision;
            MEMNodeSnapshot node=session.GetSnapshot().Find(content.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("logic.edit.foreign_content");
            MOLLogicDefinition current=Get(node);
            if(current==null)throw new InvalidOperationException("logic.edit.definition_missing");
            MOLLogicDefinition modified=change(current);
            if(modified==null)throw new InvalidOperationException("logic.edit.null_definition");
            byte[] payload=Serialize(modified);
            session.Edit("编辑结构化逻辑",revision,e=>{
                e.SetAspect(content,new MPMAspect(MPMAspectKind.Logic,
                    ProviderId,Version(modified),payload));
                return true;
            }).RequireValue();
        }
        public static byte[] Serialize(MOLLogicDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            definition.Validate();
            var root = new XElement("MOL", new XAttribute("v", Version(definition)), WriteBlock(definition.Root));
            var bytes = Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            if (bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException("logic.payload_too_large");
            return bytes;
        }
        public static MOLLogicDefinition Deserialize(byte[] payload,int expectedSchema=0)
        {
            if(expectedSchema!=0&&expectedSchema!=SchemaVersion&&
                expectedSchema!=ExtendedSchemaVersion)
                throw new InvalidDataException("logic.unsupported_schema");
            if (payload == null || payload.Length == 0 || payload.Length > 4 * 1024 * 1024)
                throw new InvalidDataException("logic.invalid_payload");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 };
            using (var input = new MemoryStream(payload))
            using (var reader = XmlReader.Create(input, settings))
            {
                var root = XDocument.Load(reader).Root;
                string version=root==null?null:(string)root.Attribute("v");
                if(root==null||root.Name!="MOL"||(version!="1"&&version!="4")||
                   (expectedSchema!=0&&version!=expectedSchema.ToString(CultureInfo.InvariantCulture)))
                    throw new InvalidDataException("logic.unsupported_schema");
                CheckSchema(root,new[]{"v"},new[]{"Block"});
                var blocks=root.Elements("Block").ToArray();
                if(blocks.Length!=1||root.Elements().Count()!=1)
                    throw new InvalidDataException("logic.invalid_root");
                MOLLogicDefinition d=new MOLLogicDefinition(ReadBlock(blocks[0],0));
                if(Version(d)!=int.Parse(version,CultureInfo.InvariantCulture))
                    throw new InvalidDataException("logic.expression_requires_v4");
                return d;
            }
        }
        /// <summary>旧Schema仍允许已定义的可选字段；未知字段拒绝而非丢弃。</summary>
        private static void CheckSchema(XElement element,string[] attributes,string[] children)
        {
            if(element.Attributes().Any(a=>a.Name.NamespaceName.Length!=0||
                    !attributes.Contains(a.Name.LocalName,StringComparer.Ordinal))||
                element.Elements().Any(e=>e.Name.NamespaceName.Length!=0||
                    !children.Contains(e.Name.LocalName,StringComparer.Ordinal)))
                throw new InvalidDataException("logic.unknown_field");
        }
        private static XElement WriteExpression(MOLExpression value)
        {
            if (value == null) return null;
            if(value.Operator!=MOLLogicalOperator.Scalar)
                return new XElement("Expression",new XAttribute("op",value.Operator),
                    value.Terms.Select(WriteExpression));
            return new XElement("Expression", new XAttribute("source", value.Source),
                new XAttribute("name", value.Name), new XAttribute("compare", value.Comparison),
                new XAttribute("expected", value.Expected ?? ""));
        }
        private static MOLExpression ReadExpression(XElement node)
        {
            if (node == null) return null;
            if(node.Name!="Expression")throw new InvalidDataException("logic.expression_schema");
            XAttribute op=node.Attribute("op");
            if(op!=null)
            {
                CheckSchema(node,new[]{"op"},new[]{"Expression"});
                MOLLogicalOperator kind;
                if(!Enum.TryParse(op.Value,false,out kind)||
                   (kind!=MOLLogicalOperator.All&&kind!=MOLLogicalOperator.Any&&
                    kind!=MOLLogicalOperator.Not))
                    throw new InvalidDataException("logic.expression_operator");
                MOLExpression[] children=node.Elements("Expression").Select(ReadExpression).ToArray();
                try
                {
                    if(kind==MOLLogicalOperator.Not)
                    {
                        if(children.Length!=1)throw new InvalidDataException("logic.expression_arity");
                        return MOLExpression.Not(children[0]);
                    }
                    return kind==MOLLogicalOperator.All?
                        MOLExpression.All(children):MOLExpression.Any(children);
                }
                catch(ArgumentException ex){throw new InvalidDataException("logic.expression_arity",ex);}
            }
            CheckSchema(node,new[]{"source","name","compare","expected"},new string[0]);
            return new MOLExpression((MOLExpressionSource)Enum.Parse(typeof(MOLExpressionSource),
                (string)node.Attribute("source")), (string)node.Attribute("name"),
                (MOLComparison)Enum.Parse(typeof(MOLComparison), (string)node.Attribute("compare")),
                (string)node.Attribute("compare") == "Truthy" ? null : (string)node.Attribute("expected"));
        }
        private static XElement WriteBlock(MOLControlBlock b)
        {
            var e = new XElement("Block", new XAttribute("id", b.Id.ToString("N")),
                new XAttribute("kind", b.Kind), new XAttribute("cap", b.CapabilityId),
                new XAttribute("target", b.TargetFlow), new XAttribute("exit", b.ExitKind),
                b.ErrorCode.Length==0?null:new XAttribute("error",b.ErrorCode));
            if (b.Condition != null) e.Add(new XElement("Guard", WriteExpression(b.Condition)));
            if (b.Wait != null)
            {
                var wait=new XElement("Wait",
                    new XAttribute("durationMs",b.Wait.DurationMilliseconds));
                if(!string.IsNullOrEmpty(b.Wait.EventInput))
                    wait.Add(new XAttribute("eventInput",b.Wait.EventInput));
                if(b.Wait.Event!=null&&!b.Wait.Event.IsLegacyTask)
                {
                    wait.Add(new XAttribute("source",b.Wait.Event.Source));
                    if(b.Wait.Event.PayloadTypeId.Length>0)
                        wait.Add(new XAttribute("payloadType",b.Wait.Event.PayloadTypeId));
                    if(b.Wait.Event.CorrelationInput.Length>0)
                        wait.Add(new XAttribute("correlation",b.Wait.Event.CorrelationInput));
                }
                e.Add(wait);
            }
            if(b.TryRegion!=null)
            {
                var region=new XElement("TryRegion",
                    new XElement("TryBody",WriteBlock(b.TryRegion.TryBody)));
                foreach(var clause in b.TryRegion.Catches)
                    region.Add(new XElement("Catch",new XAttribute("code",clause.ErrorCode),
                        WriteBlock(clause.Body)));
                if(b.TryRegion.CatchBody!=null)
                    region.Add(new XElement("CatchBody",WriteBlock(b.TryRegion.CatchBody)));
                if(b.TryRegion.FinallyBody!=null)
                    region.Add(new XElement("FinallyBody",WriteBlock(b.TryRegion.FinallyBody)));
                e.Add(region);
            }
            foreach (var c in b.Children) e.Add(WriteBlock(c));
            foreach (var c in b.Cases)
            {
                var branch = new XElement("Case", new XAttribute("label", c.Label));
                if (c.Condition != null) branch.Add(WriteExpression(c.Condition));
                branch.Add(WriteBlock(c.Body)); e.Add(branch);
            }
            if (b.Loop != null)
            {
                var l = b.Loop;
                var loop = new XElement("Loop", new XAttribute("mode", l.Mode),
                    new XAttribute("max", l.MaxIterations),
                    new XAttribute("collection", l.CollectionInput));
                if (l.Condition != null) loop.Add(WriteExpression(l.Condition));
                loop.Add(WriteBlock(l.Body)); e.Add(loop);
            }
            return e;
        }
        private static MOLControlBlock ReadBlock(XElement e, int depth)
        {
            if (depth > 32 || e.Name != "Block") throw new InvalidDataException("logic.depth_or_type");
            CheckSchema(e,new[]{"id","kind","cap","target","exit","error"},
                new[]{"Block","Case","Loop","Guard","Wait","TryRegion"});
            foreach(var guard in e.Elements("Guard"))
            {
                CheckSchema(guard,new string[0],new[]{"Expression"});
                if(guard.Elements("Expression").Count()!=1)
                    throw new InvalidDataException("logic.guard_schema");
            }
            foreach(var item in e.Elements("Case"))
            {
                CheckSchema(item,new[]{"label"},new[]{"Block","Expression"});
                if(item.Elements("Block").Count()!=1||item.Elements("Expression").Count()>1)
                    throw new InvalidDataException("logic.case_schema");
            }
            foreach(var loopNode in e.Elements("Loop"))
            {
                CheckSchema(loopNode,new[]{"mode","max","collection"},
                    new[]{"Block","Expression"});
                if(loopNode.Elements("Block").Count()!=1||
                    loopNode.Elements("Expression").Count()>1)
                    throw new InvalidDataException("logic.loop_schema");
            }
            if (e.Elements("Loop").Count() > 1 || e.Elements("Guard").Count() > 1 || e.Elements("Wait").Count()>1 ||
                e.Elements("TryRegion").Count()>1)
                throw new InvalidDataException("logic.duplicate_field");
            Guid id;
            if (!Guid.TryParseExact((string)e.Attribute("id"), "N", out id))
                throw new InvalidDataException("logic.bad_id");
            MOLControlKind kind = (MOLControlKind)Enum.Parse(typeof(MOLControlKind),
                (string)e.Attribute("kind"));
            var blocks = e.Elements("Block").Select(c => ReadBlock(c, depth + 1)).ToArray();
            var cases = e.Elements("Case").Select(c => new MOLBranchCase(
                ReadExpression(c.Element("Expression")),
                ReadBlock(c.Elements("Block").Single(), depth + 1),
                (string)c.Attribute("label"))).ToArray();
            MOLLoopDefinition loop = null;
            var le = e.Element("Loop");
            if (le != null)
                loop = new MOLLoopDefinition((MOLLoopMode)Enum.Parse(typeof(MOLLoopMode),
                    (string)le.Attribute("mode")), (int)le.Attribute("max"),
                    ReadBlock(le.Elements("Block").Single(), depth + 1),
                    ReadExpression(le.Element("Expression")), (string)le.Attribute("collection"));
            MOLWaitDefinition wait=null;
            var waitElement=e.Element("Wait");
            if(waitElement!=null)
            {
                int duration;
                string eventInput=(string)waitElement.Attribute("eventInput");
                CheckSchema(waitElement,new[]{"durationMs","eventInput","source",
                    "payloadType","correlation"},new string[0]);
                if(!int.TryParse((string)waitElement.Attribute("durationMs"),
                    NumberStyles.None,CultureInfo.InvariantCulture,out duration))
                    throw new InvalidDataException("logic.wait_payload");
                if(eventInput==null)
                {
                    if(waitElement.Attributes().Count()!=1)
                        throw new InvalidDataException("logic.wait_payload");
                    wait=new MOLWaitDefinition(duration);
                }
                else
                {
                    MOLEventSource source=MOLEventSource.FlowInput;
                    string declared=(string)waitElement.Attribute("source");
                    if(declared!=null&&(!Enum.TryParse(declared,false,out source)||
                        !Enum.IsDefined(typeof(MOLEventSource),source)))
                        throw new InvalidDataException("logic.event_source");
                    wait=new MOLWaitDefinition(new MOLEventContract(eventInput,source,
                        (string)waitElement.Attribute("payloadType"),
                        (string)waitElement.Attribute("correlation")),duration);
                }
            }
            MOLTryDefinition tryDefinition=null;
            XElement region=e.Element("TryRegion");
            if(region!=null)
            {
                CheckSchema(region,new string[0],
                    new[]{"TryBody","Catch","CatchBody","FinallyBody"});
                if(region.Elements().Count()<1||region.Elements("Catch").Count()>32||
                    region.Elements().Any(x=>x.Name!="TryBody"&&x.Name!="Catch"&&
                        x.Name!="CatchBody"&&x.Name!="FinallyBody")||
                    region.Elements("TryBody").Count()!=1||
                    region.Elements("CatchBody").Count()>1||
                    region.Elements("FinallyBody").Count()>1||
                    region.Elements().Any(x=>
                        (x.Name=="Catch"?
                            x.Attributes().Count()!=1||x.Attribute("code")==null:
                            x.Attributes().Any())||
                        x.Elements("Block").Count()!=1||x.Elements().Count()!=1))
                    throw new InvalidDataException("logic.try_schema");
                Func<string,MOLControlBlock> readPart=name=>{
                    XElement part=region.Element(name);
                    return part==null?null:ReadBlock(part.Element("Block"),depth+1);
                };
                MOLCatchClause[] clauses=region.Elements("Catch").Select(c=>
                    new MOLCatchClause((string)c.Attribute("code"),
                        ReadBlock(c.Element("Block"),depth+1))).ToArray();
                tryDefinition=new MOLTryDefinition(readPart("TryBody"),
                    readPart("CatchBody"),readPart("FinallyBody"),clauses);
            }
            return new MOLControlBlock(id, kind, (string)e.Attribute("cap"), blocks, cases,
                loop, ReadExpression(e.Element("Guard") == null ? null : e.Element("Guard").Element("Expression")),
                (string)e.Attribute("target"), (MOLExitKind)Enum.Parse(typeof(MOLExitKind),
                    (string)e.Attribute("exit")),wait,tryDefinition,
                (string)e.Attribute("error"));
        }
    }

    // V2自由连线：逻辑块是Content内的值记录，不扩展MEM工程对象树。
    public sealed class MOLBlockEndpoint : IEquatable<MOLBlockEndpoint>
    {
        public MOLBlockEndpoint(Guid blockId, string portMemberId)
        { if(blockId==Guid.Empty)throw new ArgumentException("wiring.empty_block");
          BlockId=blockId;PortMemberId=MEMNode.NormalizeName(portMemberId); }
        public Guid BlockId{get;private set;}
        public string PortMemberId{get;private set;}
        public bool Equals(MOLBlockEndpoint other)
        {return other!=null&&BlockId==other.BlockId&&PortMemberId==other.PortMemberId;}
        public override bool Equals(object obj){return Equals(obj as MOLBlockEndpoint);}
        public override int GetHashCode(){return BlockId.GetHashCode()*397^PortMemberId.GetHashCode();}
    }
    public sealed class MOLLink
    {
        public MOLLink(Guid id,MOLBlockEndpoint source,MOLBlockEndpoint target,
            MPCPortKind kind,string conditionId=null,int? order=null)
        {
            if(id==Guid.Empty||source==null||target==null||!Enum.IsDefined(typeof(MPCPortKind),kind)||
                (order.HasValue&&order.Value<0))throw new ArgumentException("wiring.invalid_link");
            Id=id;Source=source;Target=target;Kind=kind;ConditionId=conditionId??"";Order=order;
        }
        public Guid Id{get;private set;}
        public MOLBlockEndpoint Source{get;private set;}
        public MOLBlockEndpoint Target{get;private set;}
        public MPCPortKind Kind{get;private set;}
        public string ConditionId{get;private set;}
        public int? Order{get;private set;}
    }
    /// <summary>每个Content只有一个Logic槽。V2的自由连接是权威定义，不额外保存AST。</summary>
    /// <summary>编排画布位置，只负责展示；不参与MRK计划或执行依赖。</summary>
    public sealed class MOLBlockPosition
    {
        public MOLBlockPosition(double x,double y)
        {
            if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y)||
                x<0||y<0||x>100000||y>100000)
                throw new ArgumentOutOfRangeException("wiring.position");
            X=x;Y=y;
        }
        public double X {get;private set;}
        public double Y {get;private set;}
    }

    /// <summary>仅为Logic画布编辑组；没有执行语义，不等于可调用Content子流程。</summary>
    public sealed class MOLSelectionGroup
    {
        public MOLSelectionGroup(Guid id,string name,IEnumerable<Guid> members)
        {
            if(id==Guid.Empty||members==null)throw new ArgumentException("wiring.group_identity");
            Guid[] ids=members.ToArray();
            if(ids.Length<2||ids.Any(x=>x==Guid.Empty)||ids.Distinct().Count()!=ids.Length)
                throw new ArgumentException("wiring.group_members");
            Id=id;Name=MEMNode.NormalizeName(name);
            BlockIds=Array.AsReadOnly(ids);
        }
        public Guid Id{get;private set;}
        public string Name{get;private set;}
        public IReadOnlyList<Guid> BlockIds{get;private set;}
    }

    /// <summary>状态及分区是同一Logic Aspect中的值对象，不是第二棵CxNode树。</summary>
    public sealed class MOLStateDescriptor
    {
        public MOLStateDescriptor(string id,string region,string parent=null,bool initial=false,
            string entryCapability=null,string exitCapability=null)
        {
            Id=Check(id);Region=Check(region);
            ParentId=string.IsNullOrEmpty(parent)?"":Check(parent);
            if(Id==ParentId)throw new ArgumentException("moc.state.self_parent");
            Initial=initial;
            EntryCapability=string.IsNullOrEmpty(entryCapability)?"":Check(entryCapability);
            ExitCapability=string.IsNullOrEmpty(exitCapability)?"":Check(exitCapability);
        }
        internal static string Check(string input)
        {
            if(string.IsNullOrWhiteSpace(input)||input.Length>128||input.Any(char.IsControl))
                throw new ArgumentException("moc.state.bad_id");
            return input.Trim().Normalize();
        }
        public string Id{get;private set;}
        public string Region{get;private set;}
        public string ParentId{get;private set;}
        public bool Initial{get;private set;}
        /// <summary>这里只存Capability身份；实际进入退出执行与失败处理属于MRK。</summary>
        public string EntryCapability{get;private set;}
        public string ExitCapability{get;private set;}
    }
    public sealed class MOLStateTransition
    {
        public MOLStateTransition(string source,string eventId,string target,
            MOLExpression guard=null,int priority=0)
        {
            Source=MOLStateDescriptor.Check(source);
            EventId=MOLStateDescriptor.Check(eventId);
            Target=MOLStateDescriptor.Check(target);
            Action<MOLExpression> validate=null;
            validate=e=>{
                if(e==null)return;
                if(e.Operator!=MOLLogicalOperator.Scalar)
                {foreach(var t in e.Terms)validate(t);return;}
                if(e.Source!=MOLExpressionSource.FlowInput&&e.Source!=MOLExpressionSource.Constant)
                    throw new ArgumentException("moc.state.guard_source");
            };
            validate(guard);
            if(priority<0||priority>1000)
                throw new ArgumentOutOfRangeException("priority","moc.state.priority_range");
            Guard=guard;Priority=priority;
        }
        public string Source{get;private set;}
        public string EventId{get;private set;}
        public string Target{get;private set;}
        public MOLExpression Guard{get;private set;}
        /// <summary>同源同事件多条候选按优先级降序求值；同优先级冲突应拒绝。</summary>
        public int Priority{get;private set;}
    }
    /// <summary>G05d：确定性层级状态及正交Region，同一事件按Region键顺序分别转移。</summary>
    public sealed class MOLStateMachineDefinition
    {
        public IReadOnlyList<MOLStateDescriptor> States{get;private set;}
        public IReadOnlyList<MOLStateTransition> Transitions{get;private set;}
        public MOLStateMachineDefinition(IEnumerable<MOLStateDescriptor> states,
            IEnumerable<MOLStateTransition> transitions)
        {
            var entries=(states??Enumerable.Empty<MOLStateDescriptor>()).ToArray();
            var edges=(transitions??Enumerable.Empty<MOLStateTransition>()).ToArray();
            if(entries.Length==0||entries.Length>256||edges.Length>512||
                entries.Any(x=>x==null)||edges.Any(x=>x==null)||
                entries.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=entries.Length)
                throw new InvalidOperationException("moc.state.definition_budget_or_duplicate");
            var lookup=entries.ToDictionary(x=>x.Id,StringComparer.Ordinal);
            foreach(var state in entries)
            {
                var current=state;
                var ancestors=new HashSet<string>(StringComparer.Ordinal);
                while(current.ParentId.Length>0)
                {
                    if(!ancestors.Add(current.Id)||
                        !lookup.ContainsKey(current.ParentId)||
                        lookup[current.ParentId].Region!=state.Region)
                        throw new InvalidOperationException("moc.state.parent_cycle_or_region");
                    current=lookup[current.ParentId];
                }
            }
            foreach(var region in entries.GroupBy(s=>s.Region,StringComparer.Ordinal))
            {
                if(region.Count(s=>s.ParentId.Length==0&&s.Initial)!=1)
                    throw new InvalidOperationException("moc.state.missing_region_initial");
            }
            foreach(var parent in entries)
            {
                var children=entries.Where(s=>s.ParentId==parent.Id).ToArray();
                if(children.Length>0&&children.Count(s=>s.Initial)!=1)
                    throw new InvalidOperationException("moc.state.missing_nested_initial");
            }
            var transitionKeys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var edge in edges)
            {
                MOLStateDescriptor from,to;
                if(!lookup.TryGetValue(edge.Source,out from)||
                    !lookup.TryGetValue(edge.Target,out to)||from.Region!=to.Region||
                    !transitionKeys.Add(edge.Source+"\u001f"+edge.EventId+"\u001f"+
                        edge.Priority.ToString(CultureInfo.InvariantCulture)))
                    throw new InvalidOperationException("moc.state.invalid_transition");
            }
            States=Array.AsReadOnly(entries);Transitions=Array.AsReadOnly(edges);
        }
        private string ResolveLeaf(string id,IDictionary<string,MOLStateDescriptor> map)
        {
            while(true)
            {
                var initial=States.SingleOrDefault(s=>s.ParentId==id&&s.Initial);
                if(initial==null)return id;
                id=initial.Id;
            }
        }
        private static bool GuardSatisfied(MOLExpression guard,IDictionary<string,object> inputs)
        {
            return MOLScalarExpression.EvaluateTree(guard,leaf=>{
                if(leaf.Source==MOLExpressionSource.Constant)return leaf.Name;
                object value;
                if(inputs==null||!inputs.TryGetValue(leaf.Name,out value))
                    throw new InvalidOperationException("moc.state.guard_input_missing:"+leaf.Name);
                return value;
            });
        }
        /// <summary>保持旧API；需要Guard输入时显式使用新重载。</summary>
        public IReadOnlyDictionary<string,string> Run(IEnumerable<string> events,
            System.Threading.CancellationToken cancellation=
                default(System.Threading.CancellationToken))
        {return Run(events,null,cancellation);}
        /// <summary>纯状态迁移：每个事件处理一次，不持有Worker或实例运行态。</summary>
        public IReadOnlyDictionary<string,string> Run(IEnumerable<string> events,
            IDictionary<string,object> guardInputs,
            System.Threading.CancellationToken cancellation=
                default(System.Threading.CancellationToken))
        {return Run(events,guardInputs,null,cancellation);}
        /// <summary>
        /// 从调用方提供的只读状态快照继续计算。只接受完整且与当前定义一致的叶状态；
        /// 这是无状态迁移入口，不替代MRK实例生命周期或持久Checkpoint。
        /// </summary>
        public IReadOnlyDictionary<string,string> Run(IEnumerable<string> events,
            IDictionary<string,object> guardInputs,
            IReadOnlyDictionary<string,string> checkpoint,
            System.Threading.CancellationToken cancellation=
                default(System.Threading.CancellationToken))
        {
            if(events==null)throw new ArgumentNullException("events");
            var map=States.ToDictionary(s=>s.Id,StringComparer.Ordinal);
            var active=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var region in States.GroupBy(s=>s.Region,StringComparer.Ordinal))
                active.Add(region.Key,ResolveLeaf(region.Single(s=>
                    s.ParentId.Length==0&&s.Initial).Id,map));
            if(checkpoint!=null)
            {
                if(checkpoint.Count!=active.Count)
                    throw new InvalidOperationException("moc.state.invalid_checkpoint");
                foreach(string region in active.Keys.ToArray())
                {
                    string resumed;
                    MOLStateDescriptor candidate;
                    if(!checkpoint.TryGetValue(region,out resumed)||
                        !map.TryGetValue(resumed??"",out candidate)||
                        candidate.Region!=region||
                        States.Any(s=>s.ParentId==resumed))
                        throw new InvalidOperationException("moc.state.invalid_checkpoint:"+region);
                    active[region]=resumed;
                }
            }
            var rules=Transitions.GroupBy(
                x=>x.Source+"\u001f"+x.EventId,StringComparer.Ordinal)
                .ToDictionary(g=>g.Key,g=>g.OrderByDescending(x=>x.Priority).ToArray(),
                    StringComparer.Ordinal);
            int count=0;
            foreach(string eventId in events)
            {
                cancellation.ThrowIfCancellationRequested();
                if(++count>10000)throw new InvalidOperationException("moc.state.event_budget");
                string eventName=MOLStateDescriptor.Check(eventId);
                foreach(var region in active.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray())
                {
                    var current=map[active[region]];
                    while(true)
                    {
                        MOLStateTransition[] candidates;
                        bool transitioned=false;
                        if(rules.TryGetValue(current.Id+"\u001f"+eventName,out candidates))
                        {
                            foreach(var rule in candidates)
                            {
                                if(!GuardSatisfied(rule.Guard,guardInputs))continue;
                                active[region]=ResolveLeaf(rule.Target,map);
                                transitioned=true;
                                break;
                            }
                        }
                        if(transitioned)break;
                        if(current.ParentId.Length==0)break;
                        current=map[current.ParentId];
                    }
                }
            }
            return new ReadOnlyDictionary<string,string>(active);
        }
    }

    /// <summary>G05c：幂等工作流重试/超时的工程配置；执行仍归MRKExecutionPolicy。</summary>
    public sealed class MOLStepPolicy
    {
        public MOLStepPolicy(int maxAttempts=1,int retryDelayMs=0,int timeoutMs=0,
            bool idempotent=false, string executionGroup=null)
        {
            if(maxAttempts<1||maxAttempts>10||retryDelayMs<0||retryDelayMs>60000||
                timeoutMs<0||timeoutMs>600000||(maxAttempts>1&&!idempotent))
                throw new ArgumentException("wiring.invalid_workflow_policy");
            if(executionGroup!=null &&
                (executionGroup.Length>128 ||
                (executionGroup.Length>0 && string.IsNullOrWhiteSpace(executionGroup))))
                throw new ArgumentException("wiring.execution_group_invalid","executionGroup");
            MaxAttempts=maxAttempts;RetryDelayMs=retryDelayMs;TimeoutMs=timeoutMs;
            Idempotent=idempotent;
            ExecutionGroup=string.IsNullOrEmpty(executionGroup)?"" : executionGroup.Trim();
        }
        public int MaxAttempts{get;private set;}
        public int RetryDelayMs{get;private set;}
        public int TimeoutMs{get;private set;}
        public bool Idempotent{get;private set;}
        /// <summary>原MRK执行组身份；组容量/作用域只由运行期组定义管理。</summary>
        public string ExecutionGroup{get;private set;}
    }

    public sealed class MOLWiringDefinition
    {
        public MOLWiringDefinition(Guid entryBlockId,IEnumerable<MOLControlBlock> blocks,
            IDictionary<Guid,IEnumerable<MPCPortDescriptor>> ports,IEnumerable<MOLLink> links,
            IDictionary<Guid,MOLBlockPosition> positions=null,
            IEnumerable<MOLSelectionGroup> groups=null,
            IDictionary<Guid,IDictionary<string,string>> parameters=null,
            IDictionary<Guid,MOLStepPolicy> policies=null,
            IDictionary<Guid,MOLStateMachineDefinition> stateMachines=null,
            IDictionary<string,MOLBlockEndpoint> flowInputs=null,
            IDictionary<string,MOLBlockEndpoint> flowOutputs=null)
        {
            EntryBlockId=entryBlockId;
            Blocks=new ReadOnlyCollection<MOLControlBlock>((blocks??new MOLControlBlock[0]).ToArray());
            Links=new ReadOnlyCollection<MOLLink>((links??new MOLLink[0]).ToArray());
            var map=new Dictionary<Guid,IReadOnlyList<MPCPortDescriptor>>();
            if(ports!=null)foreach(var kv in ports)
                map.Add(kv.Key,new ReadOnlyCollection<MPCPortDescriptor>((kv.Value??Enumerable.Empty<MPCPortDescriptor>()).ToArray()));
            Ports=new ReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>>(map);
            var locations=new Dictionary<Guid,MOLBlockPosition>();
            if(positions!=null)foreach(var pair in positions)
            {
                if(pair.Value==null)throw new ArgumentException("wiring.null_position");
                locations.Add(pair.Key,pair.Value);
            }
            Positions=new ReadOnlyDictionary<Guid,MOLBlockPosition>(locations);
            Groups=Array.AsReadOnly((groups??Enumerable.Empty<MOLSelectionGroup>()).ToArray());
            var values=new Dictionary<Guid,IReadOnlyDictionary<string,string>>();
            if(parameters!=null)foreach(var group in parameters)
            {
                if(group.Value==null)throw new ArgumentException("wiring.params_null");
                values.Add(group.Key,new ReadOnlyDictionary<string,string>(
                    new Dictionary<string,string>(group.Value,StringComparer.Ordinal)));
            }
            Parameters=new ReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>>(values);
            var strategy=new Dictionary<Guid,MOLStepPolicy>();
            if(policies!=null)foreach(var entry in policies)
            {
                if(entry.Value==null)throw new ArgumentException("wiring.null_policy");
                strategy.Add(entry.Key,entry.Value);
            }
            Policies=new ReadOnlyDictionary<Guid,MOLStepPolicy>(strategy);
            var machines=new Dictionary<Guid,MOLStateMachineDefinition>();
            if(stateMachines!=null)foreach(var item in stateMachines)
            {
                if(item.Value==null)throw new ArgumentException("wiring.null_state_machine");
                machines.Add(item.Key,item.Value);
            }
            StateMachines=new ReadOnlyDictionary<Guid,MOLStateMachineDefinition>(machines);
            FlowInputs=new ReadOnlyDictionary<string,MOLBlockEndpoint>(
                new Dictionary<string,MOLBlockEndpoint>(flowInputs??
                    new Dictionary<string,MOLBlockEndpoint>(),StringComparer.Ordinal));
            FlowOutputs=new ReadOnlyDictionary<string,MOLBlockEndpoint>(
                new Dictionary<string,MOLBlockEndpoint>(flowOutputs??
                    new Dictionary<string,MOLBlockEndpoint>(),StringComparer.Ordinal));
            Validate();
        }
        public Guid EntryBlockId{get;private set;}
        public IReadOnlyList<MOLControlBlock> Blocks{get;private set;}
        public IReadOnlyList<MOLLink> Links{get;private set;}
        public IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> Ports{get;private set;}
        public IReadOnlyDictionary<Guid,MOLBlockPosition> Positions{get;private set;}
        public IReadOnlyList<MOLSelectionGroup> Groups{get;private set;}
        /// <summary>端口字面量参数，按BlockId绑定到已有Data Input；不是另一个参数真源。</summary>
        public IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> Parameters{get;private set;}
        public IReadOnlyDictionary<Guid,MOLStepPolicy> Policies{get;private set;}
        public IReadOnlyDictionary<Guid,MOLStateMachineDefinition> StateMachines{get;private set;}
        /// <summary>对外公开的子流程端口别名→内部节点端口（仍属同一 Logic Aspect）。</summary>
        public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowInputs{get;private set;}
        public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowOutputs{get;private set;}
        public void Validate()
        {
            // G04：合法空草稿可保存，不能交给MOC/MRK执行。
            if(Blocks.Count==0)
            {
                if(EntryBlockId!=Guid.Empty||Links.Count!=0||Ports.Count!=0||
                    Positions.Count!=0||Groups.Count!=0||Parameters.Count!=0||
                    Policies.Count!=0||StateMachines.Count!=0||FlowInputs.Count!=0||FlowOutputs.Count!=0)
                    throw new InvalidOperationException("wiring.empty_draft_has_references");
                return;
            }
            if(EntryBlockId==Guid.Empty||Blocks.Count>4096||Links.Count>32768)
                throw new InvalidOperationException("wiring.budget");
            if(Blocks.Any(x=>x==null||x.Kind!=MOLControlKind.Action||string.IsNullOrWhiteSpace(x.CapabilityId)))
                throw new InvalidOperationException("wiring.action_only");
            var ids=new HashSet<Guid>();
            foreach(var b in Blocks)if(!ids.Add(b.Id))throw new InvalidOperationException("wiring.duplicate_block");
            if(!ids.Contains(EntryBlockId)||Ports.Count!=ids.Count||Ports.Keys.Any(x=>!ids.Contains(x))||
                Positions.Keys.Any(x=>!ids.Contains(x)))
                throw new InvalidOperationException("wiring.scope");
            if(Groups.Count>1024)throw new InvalidOperationException("wiring.group_budget");
            var groupIds=new HashSet<Guid>();
            var groupedMembers=new HashSet<Guid>();
            foreach(var group in Groups)
            {
                if(group==null||!groupIds.Add(group.Id)||
                    group.BlockIds.Any(id=>!ids.Contains(id)||!groupedMembers.Add(id)))
                    throw new InvalidOperationException("wiring.group_overlap_or_scope");
            }
            if(StateMachines.Count>128||StateMachines.Keys.Any(id=>!ids.Contains(id))||
                StateMachines.Keys.Any(id=>Blocks.Single(b=>b.Id==id).CapabilityId!="moc.state-machine"))
                throw new InvalidOperationException("wiring.state_machine_scope");
            if(Policies.Count>4096||Policies.Keys.Any(id=>!ids.Contains(id)))
                throw new InvalidOperationException("wiring.policy_scope");
            if(Parameters.Keys.Any(id=>!ids.Contains(id))||
                Parameters.Sum(group=>group.Value.Count)>4096)
                throw new InvalidOperationException("wiring.parameters_scope");
            var schemas=new Dictionary<Guid,Dictionary<string,MPCPortDescriptor>>();
            foreach(var b in Blocks)
            {
                IReadOnlyList<MPCPortDescriptor> defs;
                if(!Ports.TryGetValue(b.Id,out defs))throw new InvalidOperationException("wiring.missing_schema");
                var lookup=new Dictionary<string,MPCPortDescriptor>(StringComparer.Ordinal);
                foreach(var p in defs)
                {
                    if(p==null||lookup.ContainsKey(p.MemberId))throw new InvalidOperationException("wiring.duplicate_port");
                    lookup.Add(p.MemberId,p);
                }
                schemas.Add(b.Id,lookup);
            }
            if(FlowInputs.Count+FlowOutputs.Count>256)
                throw new InvalidOperationException("wiring.public_ports_budget");
            Action<IReadOnlyDictionary<string,MOLBlockEndpoint>,MPCPortDirection> checkPublic =
                (publicPorts,direction)=>{
                    foreach(var exposed in publicPorts)
                    {
                        string name=exposed.Key;
                        if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.Contains(".")||
                           name.Any(char.IsControl)||exposed.Value==null)
                            throw new InvalidOperationException("wiring.invalid_public_port");
                        Dictionary<string,MPCPortDescriptor> registered;
                        MPCPortDescriptor port;
                        if(!schemas.TryGetValue(exposed.Value.BlockId,out registered)||
                           !registered.TryGetValue(exposed.Value.PortMemberId,out port)||
                           port.Kind!=MPCPortKind.Data||port.Direction!=direction)
                            throw new InvalidOperationException("wiring.public_port_contract");
                        if(direction==MPCPortDirection.Input &&
                           (Links.Any(l=>(l.Kind==MPCPortKind.Data||l.Kind==MPCPortKind.Stream)&&
                               l.Target.Equals(exposed.Value)) ||
                            (Parameters.ContainsKey(exposed.Value.BlockId)&&
                             Parameters[exposed.Value.BlockId].ContainsKey(exposed.Value.PortMemberId))))
                            throw new InvalidOperationException("wiring.public_input_conflict");
                    }
                };
            checkPublic(FlowInputs,MPCPortDirection.Input);
            checkPublic(FlowOutputs,MPCPortDirection.Output);
            if(FlowInputs.Values.GroupBy(x=>x).Any(x=>x.Count()!=1))
                throw new InvalidOperationException("wiring.public_input_duplicate_target");
            foreach(var group in Parameters)
            foreach(var entry in group.Value)
            {
                MPCPortDescriptor port;
                if(string.IsNullOrWhiteSpace(entry.Key)||entry.Key.Length>128||
                    entry.Key.Any(char.IsControl)||entry.Value==null||
                    entry.Value.Length>8192||
                    !schemas[group.Key].TryGetValue(entry.Key,out port)||
                    port.Kind!=MPCPortKind.Data||port.Direction!=MPCPortDirection.Input)
                    throw new InvalidOperationException("wiring.invalid_parameter");
                if(Links.Any(l=>l.Kind==MPCPortKind.Data&&
                    l.Target.BlockId==group.Key&&l.Target.PortMemberId==entry.Key))
                    throw new InvalidOperationException("wiring.parameter_link_conflict");
                switch(port.TypeId)
                {
                    case "string":case "System.String":break;
                    case "bool":case "System.Boolean":
                        bool b;if(!bool.TryParse(entry.Value,out b))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    case "int":case "System.Int32":
                        int iv;if(!int.TryParse(entry.Value,NumberStyles.Integer,
                            CultureInfo.InvariantCulture,out iv))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    case "long":case "System.Int64":
                        long lv;if(!long.TryParse(entry.Value,NumberStyles.Integer,
                            CultureInfo.InvariantCulture,out lv))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    case "double":case "System.Double":
                        double dv;if(!double.TryParse(entry.Value,NumberStyles.Float,
                            CultureInfo.InvariantCulture,out dv)||double.IsInfinity(dv)||
                            double.IsNaN(dv))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    default:throw new InvalidOperationException("wiring.parameter_unknown_type");
                }
            }
            var linkIds=new HashSet<Guid>();
            var edgeKeys=new HashSet<string>();
            var incoming=new Dictionary<string,int>();
            var successors=ids.ToDictionary(x=>x,x=>new List<Guid>());
            var controlSuccessors=ids.ToDictionary(x=>x,x=>new List<Guid>());
            foreach(var link in Links)
            {
                if(link==null||!linkIds.Add(link.Id))throw new InvalidOperationException("wiring.duplicate_link");
                if((link.Kind!=MPCPortKind.Control&&link.Kind!=MPCPortKind.Data&&
                     link.Kind!=MPCPortKind.Stream)||
                    link.Order.HasValue||link.ConditionId.Length>128||
                    (link.Kind!=MPCPortKind.Control&&!string.IsNullOrEmpty(link.ConditionId))||
                    (!string.IsNullOrEmpty(link.ConditionId)&&
                     link.ConditionId!="bool:true"&&link.ConditionId!="bool:false"&&
                     link.ConditionId!="default"&&
                     !(link.ConditionId.StartsWith("case:",StringComparison.Ordinal)&&
                       link.ConditionId.Length>5)))
                    throw new InvalidOperationException("wiring.unsupported_semantics");
                if(!ids.Contains(link.Source.BlockId)||!ids.Contains(link.Target.BlockId)||
                    link.Source.BlockId==link.Target.BlockId)
                    throw new InvalidOperationException("wiring.foreign_endpoint");
                MPCPortDescriptor from,to;
                if(!schemas[link.Source.BlockId].TryGetValue(link.Source.PortMemberId,out from)||
                   !schemas[link.Target.BlockId].TryGetValue(link.Target.PortMemberId,out to))
                    throw new InvalidOperationException("wiring.unknown_port");
                if(from.Kind!=link.Kind||to.Kind!=link.Kind||
                    from.Direction==MPCPortDirection.Input||to.Direction==MPCPortDirection.Output||
                    !string.Equals(from.TypeId,to.TypeId,StringComparison.Ordinal)||
                    !object.Equals(from.Contract,to.Contract))
                    throw new InvalidOperationException("wiring.port_contract");
                string edge=link.Source.BlockId+":"+link.Source.PortMemberId+">"+
                    link.Target.BlockId+":"+link.Target.PortMemberId;
                if(!edgeKeys.Add(edge))throw new InvalidOperationException("wiring.duplicate_edge");
                string toKey=link.Target.BlockId+":"+link.Target.PortMemberId;
                int n;incoming.TryGetValue(toKey,out n);incoming[toKey]=++n;
                if(n>1&&to.Multiplicity==MPCPortMultiplicity.One)
                    throw new InvalidOperationException("wiring.port_multiplicity");
                successors[link.Source.BlockId].Add(link.Target.BlockId);
                if(link.Kind==MPCPortKind.Control)controlSuccessors[link.Source.BlockId].Add(link.Target.BlockId);
            }
            // 同一条件源不得混用无条件出口；Switch case/default必须互斥。
            foreach(var group in Links.Where(l=>l.Kind==MPCPortKind.Control)
                .GroupBy(l=>l.Source.BlockId))
            {
                var guarded=group.Where(l=>l.ConditionId.Length>0).ToArray();
                if(guarded.Length==0)continue;
                if(guarded.Length!=group.Count()||
                    guarded.Select(l=>l.ConditionId).Distinct(StringComparer.Ordinal).Count()!=guarded.Length||
                    (guarded.Any(l=>l.ConditionId.StartsWith("bool:",StringComparison.Ordinal))&&
                     guarded.Any(l=>!l.ConditionId.StartsWith("bool:",StringComparison.Ordinal)))||
                    (guarded.Any(l=>l.ConditionId.StartsWith("case:",StringComparison.Ordinal)||l.ConditionId=="default")&&
                     guarded.Any(l=>l.ConditionId.StartsWith("bool:",StringComparison.Ordinal))))
                    throw new InvalidOperationException("wiring.invalid_branch_guards");
            }
            // 未完成连线的编辑草稿可以保存；构建/运行时由ValidateExecutable拒绝。
            // 连接类型、方向、重复、环路等无效结构仍严格拒绝。
            var colors=ids.ToDictionary(x=>x,x=>0);
            Action<Guid> visit=null;
            visit=id=>{
                if(colors[id]==1)throw new InvalidOperationException("wiring.cycle");
                if(colors[id]==2)return;
                colors[id]=1;foreach(var next in successors[id])visit(next);colors[id]=2;
            };
            foreach(var id in ids)visit(id);
        }
        /// <summary>只有可执行的完整自由连线才能提交给MOC/MRK，草稿不能运行。</summary>
        public void ValidateExecutable()
        {
            Validate();
            if(Blocks.Count==0)throw new InvalidOperationException("wiring.empty_executable");
            if(Links.Where(l=>l.Kind==MPCPortKind.Control&&l.ConditionId.Length>0)
                .Any(l=>Links.Count(x=>x.Kind==MPCPortKind.Control&&
                    x.Target.BlockId==l.Target.BlockId)>1))
                throw new InvalidOperationException("wiring.conditional_join_requires_explicit_node");
            var incoming=Links.GroupBy(x=>x.Target.BlockId.ToString("N")+":"+x.Target.PortMemberId)
                .ToDictionary(x=>x.Key,x=>x.Count(),StringComparer.Ordinal);
            foreach(var b in Blocks)foreach(var port in Ports[b.Id])
                if(port.Required&&port.Direction!=MPCPortDirection.Output&&
                    !incoming.ContainsKey(b.Id.ToString("N")+":"+port.MemberId)&&
                    !FlowInputs.Values.Any(x=>x.BlockId==b.Id&&x.PortMemberId==port.MemberId)&&
                    !(port.Kind==MPCPortKind.Data&&Parameters.ContainsKey(b.Id)&&
                      Parameters[b.Id].ContainsKey(port.MemberId)))
                    throw new InvalidOperationException("wiring.required_port:"+b.Id+":"+port.MemberId);
            var reached=new HashSet<Guid>();
            var pending=new Queue<Guid>();pending.Enqueue(EntryBlockId);
            var controls=Links.Where(x=>x.Kind==MPCPortKind.Control)
                .ToLookup(x=>x.Source.BlockId,x=>x.Target.BlockId);
            while(pending.Count>0)
            {
                var id=pending.Dequeue();
                if(!reached.Add(id))continue;
                foreach(var next in controls[id])pending.Enqueue(next);
            }
            if(reached.Count!=Blocks.Count)
                throw new InvalidOperationException("wiring.unreachable_action");
        }
    }
    /// <summary>V1(cx.logic.structured/1)原样保留；V2在同一Logic Aspect中原子替换。</summary>
    public static class MOLWiringCodec
    {
        public const int SchemaVersion=2;
        // 同一V2连线表示的扩展版本，不创建第三种工程Graph。
        public const int ExtendedSchemaVersion=3;
        public const int CompoundSchemaVersion=5;
        private static bool NeedsCompoundSchema(MOLWiringDefinition definition)
        {return definition.StateMachines.Values.Any(m=>m.Transitions.Any(t=>
            t.Guard!=null&&t.Guard.Operator!=MOLLogicalOperator.Scalar));}
        private static int Version(MOLWiringDefinition definition)
        {return NeedsCompoundSchema(definition)?CompoundSchemaVersion:
            NeedsExtendedSchema(definition)?ExtendedSchemaVersion:SchemaVersion;}
        private static bool NeedsExtendedSchema(MOLWiringDefinition definition)
        {
            return definition.StateMachines.Values.Any(m=>m.States.Any(s=>
                s.EntryCapability.Length>0||s.ExitCapability.Length>0));
        }
        public static MPMAspect CreateAspect(MOLWiringDefinition definition)
        {
            if(definition==null)throw new ArgumentNullException("definition");
            return new MPMAspect(MPMAspectKind.Logic,MOLLogicCodec.ProviderId,
            Version(definition),Serialize(definition));
        }
        /// <summary>仅无损转换单一V1 Action；复杂结构返回明确信息并保持原定义。</summary>
        public static void ConvertFromV1(MEMEngineeringSession session,MEMContent content)
        {
            if(session==null||content==null)throw new ArgumentNullException("session/content");
            int revision=session.Revision;
            MEMNodeSnapshot node=session.GetSnapshot().Find(content.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("wiring.convert.foreign_content");
            MOLLogicDefinition structured=MOLLogicCodec.Get(node);
            if(structured==null)throw new InvalidOperationException("wiring.convert.requires_v1");
            MOLControlBlock action=structured.Root;
            if(action.Kind!=MOLControlKind.Action||action.Condition!=null||
                action.TargetFlow.Length!=0||action.ExitKind!=MOLExitKind.Return||
                action.Loop!=null||action.Wait!=null||action.TryRegion!=null||
                action.Children.Count!=0||action.Cases.Count!=0)
                throw new InvalidOperationException("wiring.convert.not_lossless");
            var schema=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>{
                {action.Id,new MPCPortDescriptor[0]}};
            var wired=new MOLWiringDefinition(action.Id,new[]{action},schema,new MOLLink[0]);
            byte[] payload=Serialize(wired);
            session.Edit("V1单动作无损转换到V2",revision,e=>{
                e.SetAspect(content,new MPMAspect(MPMAspectKind.Logic,
                    MOLLogicCodec.ProviderId,SchemaVersion,payload));return true;
            }).RequireValue();
        }
        public static void Set(MEMEngineeringSession session,MEMContent content,MOLWiringDefinition definition)
        {if(session==null||content==null)throw new ArgumentNullException("session/content");
         session.SetAspect(content,CreateAspect(definition));}
        public static MOLWiringDefinition Get(MEMNodeSnapshot node)
        {
            if(node==null)throw new ArgumentNullException("node");
            var found=node.Aspects.SingleOrDefault(x=>x.Kind==MPMAspectKind.Logic&&x.ProviderId==MOLLogicCodec.ProviderId);
            if(found==null||found.SchemaVersion==MOLLogicCodec.SchemaVersion||
                found.SchemaVersion==MOLLogicCodec.ExtendedSchemaVersion)return null;
            if((found.SchemaVersion!=SchemaVersion&&
                found.SchemaVersion!=ExtendedSchemaVersion&&
                found.SchemaVersion!=CompoundSchemaVersion)||found.RequiresRemap)
                throw new InvalidDataException("wiring.unknown_schema");
            return Deserialize(found.Payload,found.SchemaVersion);
        }
        private static XElement WriteStateGuard(MOLExpression expr)
        {
            if(expr.Operator!=MOLLogicalOperator.Scalar)
                return new XElement("Guard",new XAttribute("op",expr.Operator),
                    expr.Terms.Select(WriteStateGuard));
            return new XElement("Guard",new XAttribute("source",expr.Source),
                new XAttribute("name",expr.Name),
                new XAttribute("compare",expr.Comparison),
                new XAttribute("expected",expr.Expected??""));
        }
        private static MOLExpression ReadStateGuard(XElement guard,int depth)
        {
            if(depth>16||guard.Name!="Guard")
                throw new InvalidDataException("wiring.state.guard_depth");
            XAttribute op=guard.Attribute("op");
            if(op==null)
            {
                if(guard.HasElements||guard.Attributes().Count()!=4||
                    guard.Attributes().Any(a=>a.Name!="source"&&a.Name!="name"&&
                        a.Name!="compare"&&a.Name!="expected"))
                    throw new InvalidDataException("wiring.state.guard_fields");
                string compare=(string)guard.Attribute("compare");
                return new MOLExpression(ReadEnum<MOLExpressionSource>(guard,"source"),
                    (string)guard.Attribute("name"),
                    ReadEnum<MOLComparison>(guard,"compare"),
                    compare=="Truthy"?null:(string)guard.Attribute("expected"));
            }
            if(guard.Attributes().Count()!=1||
                guard.Elements().Any(x=>x.Name!="Guard"))
                throw new InvalidDataException("wiring.state.guard_schema");
            MOLLogicalOperator kind;
            if(!Enum.TryParse(op.Value,false,out kind)||
                (kind!=MOLLogicalOperator.All&&kind!=MOLLogicalOperator.Any&&
                 kind!=MOLLogicalOperator.Not))
                throw new InvalidDataException("wiring.state.guard_operator");
            MOLExpression[] terms=guard.Elements("Guard").Select(x=>
                ReadStateGuard(x,depth+1)).ToArray();
            try
            {
                if(kind==MOLLogicalOperator.Not)
                {
                    if(terms.Length!=1)throw new InvalidDataException("wiring.state.guard_arity");
                    return MOLExpression.Not(terms[0]);
                }
                return kind==MOLLogicalOperator.All?
                    MOLExpression.All(terms):MOLExpression.Any(terms);
            }
            catch(ArgumentException ex)
            {throw new InvalidDataException("wiring.state.guard_arity",ex);}
        }
        public static byte[] Serialize(MOLWiringDefinition def)
        {
            if(def==null)throw new ArgumentNullException("def");def.Validate();
            var root=new XElement("MOL",
                new XAttribute("v",Version(def)),
                new XAttribute("entry",def.EntryBlockId.ToString("N")),
                new XElement("Blocks",def.Blocks.Select(b=>new XElement("Block",
                    new XAttribute("id",b.Id.ToString("N")),new XAttribute("cap",b.CapabilityId),
                    def.Positions.ContainsKey(b.Id)?
                        new XAttribute("x",def.Positions[b.Id].X.ToString("R",CultureInfo.InvariantCulture)):null,
                    def.Positions.ContainsKey(b.Id)?
                        new XAttribute("y",def.Positions[b.Id].Y.ToString("R",CultureInfo.InvariantCulture)):null,
                    new XElement("Ports",def.Ports[b.Id].Select(p=>new XElement("Port",
                        new XAttribute("id",p.MemberId),new XAttribute("dir",p.Direction),
                        new XAttribute("type",p.TypeId),new XAttribute("kind",p.Kind),
                        new XAttribute("mul",p.Multiplicity),new XAttribute("required",p.Required),
                        new XAttribute("unit",p.Contract.UnitId),
                        new XAttribute("frame",p.Contract.CoordinateFrameId),
                        new XAttribute("time",p.Contract.TimeBaseId))))))),
                new XElement("Links",def.Links.Select(l=>new XElement("Link",
                    new XAttribute("id",l.Id.ToString("N")),new XAttribute("kind",l.Kind),
                    new XAttribute("source",l.Source.BlockId.ToString("N")),
                    new XAttribute("out",l.Source.PortMemberId),
                    new XAttribute("target",l.Target.BlockId.ToString("N")),
                    new XAttribute("in",l.Target.PortMemberId),
                    new XAttribute("condition",l.ConditionId),
                    new XAttribute("order",l.Order.HasValue?l.Order.Value.ToString(CultureInfo.InvariantCulture):"")))));
            if(def.StateMachines.Count>0)
                root.Add(new XElement("StateMachines",def.StateMachines.Select(item=>
                    new XElement("Machine",new XAttribute("block",item.Key.ToString("N")),
                        new XElement("States",item.Value.States.Select(state=>
                            new XElement("State",new XAttribute("id",state.Id),
                                new XAttribute("region",state.Region),
                                new XAttribute("parent",state.ParentId),
                                new XAttribute("initial",state.Initial),
                                state.EntryCapability.Length==0?null:
                                    new XAttribute("onEntry",state.EntryCapability),
                                state.ExitCapability.Length==0?null:
                                    new XAttribute("onExit",state.ExitCapability)))),
                        new XElement("Transitions",item.Value.Transitions.Select(edge=>
                            new XElement("Transition",new XAttribute("from",edge.Source),
                                new XAttribute("event",edge.EventId),
                                new XAttribute("to",edge.Target),
                                edge.Priority==0?null:new XAttribute("priority",edge.Priority),
                                edge.Guard==null?null:WriteStateGuard(edge.Guard))))))));
            if(def.Policies.Count>0)
                root.Add(new XElement("Policies",def.Policies.Select(item=>
                    new XElement("Policy",new XAttribute("block",item.Key.ToString("N")),
                        new XAttribute("attempts",item.Value.MaxAttempts),
                        new XAttribute("delay",item.Value.RetryDelayMs),
                        new XAttribute("timeout",item.Value.TimeoutMs),
                        new XAttribute("idempotent",item.Value.Idempotent),
                        item.Value.ExecutionGroup.Length==0?null:
                            new XAttribute("group",item.Value.ExecutionGroup)))));
            if(def.Parameters.Count>0)
                root.Add(new XElement("Parameters",def.Parameters.Select(group=>
                    new XElement("Block",new XAttribute("id",group.Key.ToString("N")),
                        group.Value.Select(item=>new XElement("Value",
                            new XAttribute("port",item.Key),
                            new XAttribute("text",item.Value)))))));
            if(def.FlowInputs.Count>0)
                root.Add(new XElement("FlowInputs",def.FlowInputs.Select(x=>
                    new XElement("Port",new XAttribute("name",x.Key),
                        new XAttribute("block",x.Value.BlockId.ToString("N")),
                        new XAttribute("member",x.Value.PortMemberId)))));
            if(def.FlowOutputs.Count>0)
                root.Add(new XElement("FlowOutputs",def.FlowOutputs.Select(x=>
                    new XElement("Port",new XAttribute("name",x.Key),
                        new XAttribute("block",x.Value.BlockId.ToString("N")),
                        new XAttribute("member",x.Value.PortMemberId)))));
            if(def.Groups.Count>0)
                root.Add(new XElement("Groups",def.Groups.Select(g=>new XElement("Group",
                    new XAttribute("id",g.Id.ToString("N")),new XAttribute("name",g.Name),
                    g.BlockIds.Select(id=>new XElement("Member",
                        new XAttribute("id",id.ToString("N"))))))));
            byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length>4*1024*1024)throw new InvalidDataException("wiring.payload_budget");
            return bytes;
        }
        public static MOLWiringDefinition Deserialize(byte[] bytes,int expectedSchema=0)
        {
            if(expectedSchema!=0&&expectedSchema!=SchemaVersion&&
                expectedSchema!=ExtendedSchemaVersion&&expectedSchema!=CompoundSchemaVersion)
                throw new InvalidDataException("wiring.unknown_schema");
            if(bytes==null||bytes.Length<1||bytes.Length>4*1024*1024)throw new InvalidDataException("wiring.payload_invalid");
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024};
            using(var memory=new MemoryStream(bytes))using(var reader=XmlReader.Create(memory,settings))
            {
                var root=XDocument.Load(reader).Root;
                string version=root==null?null:(string)root.Attribute("v");
                if(root==null||root.Name!="MOL"||
                    (version!="2"&&version!="3"&&version!="5")||
                    (expectedSchema!=0&&version!=expectedSchema.ToString(CultureInfo.InvariantCulture))||
                    root.Element("Blocks")==null||root.Element("Links")==null||
                    root.Elements().Count()<2||root.Elements().Count()>8||
                    root.Elements().Any(x=>x.Name!="Blocks"&&x.Name!="Links"&&
                        x.Name!="Groups"&&x.Name!="Parameters"&&x.Name!="Policies"&&
                        x.Name!="StateMachines"&&x.Name!="FlowInputs"&&x.Name!="FlowOutputs")||
                    root.Elements().GroupBy(x=>x.Name).Any(x=>x.Count()!=1))
                    throw new InvalidDataException("wiring.schema");
                string rawEntry=(string)root.Attribute("entry");
                Guid entry=root.Element("Blocks").Elements("Block").Any()?
                    ReadGuid(root,"entry"):
                    rawEntry==Guid.Empty.ToString("N")?Guid.Empty:ReadGuid(root,"entry");
                var blocks=new List<MOLControlBlock>();
                var ports=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
                var positions=new Dictionary<Guid,MOLBlockPosition>();
                foreach(var b in root.Element("Blocks").Elements("Block"))
                {
                    if(b.Element("Ports")==null||b.Elements().Count()!=1)
                        throw new InvalidDataException("wiring.block_schema");
                    Guid id=ReadGuid(b,"id");
                    string sx=(string)b.Attribute("x"),sy=(string)b.Attribute("y");
                    if(sx!=null||sy!=null)
                    {
                        double x,y;
                        if(sx==null||sy==null||
                            !double.TryParse(sx,NumberStyles.Float,CultureInfo.InvariantCulture,out x)||
                            !double.TryParse(sy,NumberStyles.Float,CultureInfo.InvariantCulture,out y))
                            throw new InvalidDataException("wiring.invalid_position");
                        positions.Add(id,new MOLBlockPosition(x,y));
                    }
                    blocks.Add(new MOLControlBlock(id,MOLControlKind.Action,(string)b.Attribute("cap")));
                    ports.Add(id,b.Element("Ports").Elements("Port").Select(p=>
                        new MPCPortDescriptor((string)p.Attribute("id"),ReadEnum<MPCPortDirection>(p,"dir"),
                            (string)p.Attribute("type"),ReadEnum<MPCPortKind>(p,"kind"),
                            ReadEnum<MPCPortMultiplicity>(p,"mul"),
                            new MTUContractReference((string)p.Attribute("unit"),(string)p.Attribute("frame"),
                                (string)p.Attribute("time")),false,(bool?)p.Attribute("required")??false)).ToArray());
                }
                var links=root.Element("Links").Elements("Link").Select(l=>new MOLLink(
                    ReadGuid(l,"id"),new MOLBlockEndpoint(ReadGuid(l,"source"),(string)l.Attribute("out")),
                    new MOLBlockEndpoint(ReadGuid(l,"target"),(string)l.Attribute("in")),
                    ReadEnum<MPCPortKind>(l,"kind"),(string)l.Attribute("condition"),
                    string.IsNullOrEmpty((string)l.Attribute("order"))?(int?)null:
                        int.Parse((string)l.Attribute("order"),CultureInfo.InvariantCulture))).ToArray();
                var groups=root.Element("Groups")==null?new MOLSelectionGroup[0]:
                    root.Element("Groups").Elements("Group").Select(g=>
                        new MOLSelectionGroup(ReadGuid(g,"id"),(string)g.Attribute("name"),
                            g.Elements("Member").Select(m=>ReadGuid(m,"id")))).ToArray();
                var parameters=new Dictionary<Guid,IDictionary<string,string>>();
                if(root.Element("Parameters")!=null)
                foreach(var group in root.Element("Parameters").Elements("Block"))
                {
                    Guid id=ReadGuid(group,"id");
                    var values=new Dictionary<string,string>(StringComparer.Ordinal);
                    foreach(var p in group.Elements("Value"))
                    {
                        string name=(string)p.Attribute("port");
                        if(string.IsNullOrWhiteSpace(name)||p.Attribute("text")==null)
                            throw new InvalidDataException("wiring.parameter_schema");
                        values.Add(name,(string)p.Attribute("text"));
                    }
                    parameters.Add(id,values);
                }
                var policies=new Dictionary<Guid,MOLStepPolicy>();
                if(root.Element("Policies")!=null)
                foreach(var policy in root.Element("Policies").Elements("Policy"))
                {
                    Guid id=ReadGuid(policy,"block");
                    var instance=new MOLStepPolicy(
                        int.Parse((string)policy.Attribute("attempts"),CultureInfo.InvariantCulture),
                        int.Parse((string)policy.Attribute("delay"),CultureInfo.InvariantCulture),
                        int.Parse((string)policy.Attribute("timeout"),CultureInfo.InvariantCulture),
                        bool.Parse((string)policy.Attribute("idempotent")),
                        (string)policy.Attribute("group"));
                    policies.Add(id,instance);
                }
                var stateMachines=new Dictionary<Guid,MOLStateMachineDefinition>();
                if(root.Element("StateMachines")!=null)
                foreach(var machine in root.Element("StateMachines").Elements("Machine"))
                {
                    if(machine.Element("States")==null||machine.Element("Transitions")==null||
                        machine.Elements().Count()!=2)
                        throw new InvalidDataException("wiring.state_machine_schema");
                    Guid id=ReadGuid(machine,"block");
                    var states=machine.Element("States").Elements("State").Select(state=>{
                        if(state.Attributes().Any(a=>a.Name!="id"&&a.Name!="region"&&
                           a.Name!="parent"&&a.Name!="initial"&&
                           a.Name!="onEntry"&&a.Name!="onExit"))
                            throw new InvalidDataException("wiring.state.unknown_field");
                        if(version=="2"&&(state.Attribute("onEntry")!=null||
                            state.Attribute("onExit")!=null))
                            throw new InvalidDataException("wiring.state_action_requires_v3");
                        return new MOLStateDescriptor((string)state.Attribute("id"),
                            (string)state.Attribute("region"),(string)state.Attribute("parent"),
                            (bool?)state.Attribute("initial")??false,
                            (string)state.Attribute("onEntry"),
                            (string)state.Attribute("onExit"));
                    }).ToArray();
                    var transitions=machine.Element("Transitions").Elements("Transition").Select(edge=>{
                        if(edge.Elements().Any(x=>x.Name!="Guard")||
                            edge.Elements("Guard").Count()>1)
                            throw new InvalidDataException("wiring.state.guard_schema");
                        XElement guard=edge.Element("Guard");
                        MOLExpression condition=null;
                        if(guard!=null)
                        {
                            if(version!="5"&&guard.Attribute("op")!=null)
                                throw new InvalidDataException("wiring.state.guard_requires_v5");
                            condition=ReadStateGuard(guard,0);
                        }
                        int priority=0;
                        XAttribute rawPriority=edge.Attribute("priority");
                        if(rawPriority!=null&&
                            (!int.TryParse(rawPriority.Value,NumberStyles.Integer,
                                CultureInfo.InvariantCulture,out priority)||
                                priority<0||priority>1000))
                            throw new InvalidDataException("wiring.state.priority");
                        return new MOLStateTransition((string)edge.Attribute("from"),
                            (string)edge.Attribute("event"),(string)edge.Attribute("to"),
                            condition,priority);
                    }).ToArray();
                    stateMachines.Add(id,new MOLStateMachineDefinition(states,transitions));
                }
                Func<string,Dictionary<string,MOLBlockEndpoint>> readPublic=tag=>{
                    var result=new Dictionary<string,MOLBlockEndpoint>(StringComparer.Ordinal);
                    XElement node=root.Element(tag);
                    if(node!=null)
                    {
                        if(node.Elements().Any(x=>x.Name!="Port"))throw new InvalidDataException("wiring.public_port_schema");
                        foreach(var item in node.Elements("Port"))
                        {
                            string name=(string)item.Attribute("name");
                            if(string.IsNullOrWhiteSpace(name))throw new InvalidDataException("wiring.public_port_name");
                            result.Add(name,new MOLBlockEndpoint(ReadGuid(item,"block"),
                                (string)item.Attribute("member")));
                        }
                    }
                    return result;
                };
                return new MOLWiringDefinition(entry,blocks,ports,links,positions,groups,parameters,
                    policies,stateMachines,readPublic("FlowInputs"),readPublic("FlowOutputs"));
            }
        }
        private static Guid ReadGuid(XElement x,string name)
        {Guid id;if(!Guid.TryParseExact((string)x.Attribute(name),"N",out id)||id==Guid.Empty)
            throw new InvalidDataException("wiring.guid:"+name);return id;}
        private static T ReadEnum<T>(XElement x,string name)where T:struct
        {T v;if(!Enum.TryParse<T>((string)x.Attribute(name),false,out v)||!Enum.IsDefined(typeof(T),v))
            throw new InvalidDataException("wiring.enum:"+name);return v;}
    }

}
