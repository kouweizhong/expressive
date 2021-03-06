﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

using Expressive.Abstraction;
using Expressive.Decompilation;
using Expressive.Decompilation.Pipelines;
using Expressive.Decompilation.Steps.StatementInlining;
using Expressive.Disassembly.Instructions;
using Expressive.Elements;
using MbUnit.Framework;

namespace Expressive.Tests.Massive {
    [TestFixture]
    public class InstructionSupportTest {
        private static readonly HashSet<OpCode> UnsupportedOpCodes = new HashSet<OpCode> {
            OpCodes.Leave_S,
            OpCodes.Stfld,
            OpCodes.Switch,
            OpCodes.Throw,
            OpCodes.Endfinally
        };

        [Test]
        [Ignore("Manual only for now")]
        [Factory("GetAllMethodsThatHaveBodies")]
        public void TestAllInstructionsCanBeDisassembled(IManagedMethod method) {
            var disassembler = ExpressiveEngine.GetDisassembler();
            Assert.DoesNotThrow(() => disassembler.Disassemble(method));
        }

        [Test]
        [Ignore("Not passing yet")]
        public void TestAllInstructionsExceptSpecificOnesAreProcessed() {
            var pipeline = new DefaultPipeline().Without<LambdaInliningVisitor>();
            var disassembler = ExpressiveEngine.GetDisassembler();
            var visitor = new InstructionCollectingVisitor();

            foreach (var method in GetAllNonGenericMethods()) {
                var elements = disassembler.Disassemble(method)
                                           .Select(i => (IElement)new InstructionElement(i)).ToList();
                try { ApplyPipeline(pipeline, elements, method); } catch { continue; }
                visitor.VisitList(elements);
            }

            Assert.AreElementsEqual(
                new OpCode[0],
                visitor.OpCodes.Except(UnsupportedOpCodes).OrderBy(code => code.Name)
            );
        }
        
        [Test]
        [Ignore("Manual only for now")]
        [Factory("GetAllSupportedMethods")]
        public void TestNoExceptionsAreThrownWhenDecompiling(IManagedMethod method, IList<Instruction> instructions) {
            var pipeline = new DefaultPipeline().Without<LambdaInliningVisitor>();
            var elements = instructions.Select(i => (IElement)new InstructionElement(i)).ToList();
            Assert.DoesNotThrow(() => {
                try {
                    ApplyPipeline(pipeline, elements, method);
                }
                catch (NotSupportedException) {
                }
            });
        }

        private IEnumerable<object[]> GetAllSupportedMethods() {
            var disassembler = ExpressiveEngine.GetDisassembler();
            return GetAllNonGenericMethods()
                        .Select(method => new { method, instructions = disassembler.Disassemble(method).ToList() })
                        .Where(x => !x.instructions.Any(i => UnsupportedOpCodes.Contains(i.OpCode)))
                        .Select(x => new object[] { x.method, x.instructions });
        }

        private IEnumerable<IManagedMethod> GetAllNonGenericMethods() {
            return GetAllMethodsRaw().Where(m => !m.IsGenericMethodDefinition)
                                     .Select(method => new MethodBaseAdapter(method));
        }

        private IEnumerable<IManagedMethod> GetAllMethodsThatHaveBodies() {
            return GetAllMethodsRaw().Where(m => m.GetMethodBody() != null)
                                     .Select(method => new MethodBaseAdapter(method));
        }

        private static IEnumerable<MethodInfo> GetAllMethodsRaw() {
            return typeof(string).Assembly.GetTypes()
                                .SelectMany(t => t.GetMethods());
        }

        private static void ApplyPipeline(IDecompilationPipeline pipeline, IList<IElement> elements, IManagedMethod method) {
            var parameters = method.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToList();
            if (!method.IsStatic)
                parameters.Insert(0, Expression.Parameter(method.DeclaringType, "<this>"));

            var context = new DecompilationContext(null, method, i => parameters[i]);
            foreach (var step in pipeline.GetSteps()) {
                step.Apply(elements, context);
            }
        }
    }
}
