﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using AshMind.Extensions;

using Expressive.Elements;

namespace Expressive.Pipeline.Steps {
    public class BrResolutionStep : IInterpretationStep {
        private static readonly IDictionary<OpCode, bool> conditionalJumps = new Dictionary<OpCode, bool> {
            { OpCodes.Brfalse,   false },
            { OpCodes.Brfalse_S, false },
            { OpCodes.Brtrue,    true },
            { OpCodes.Brtrue_S,  true }
        };

        public void Apply(IList<IElement> elements, InterpretationContext context) {
            for (var i = 0; i < elements.Count; i++) {
                var element = elements[i];
                if (!BrProcessing.In(element, conditionalJumps.Keys.ToArray()))
                    continue;

                var targetIndex = BrProcessing.FindTargetIndexOrNull(element, elements);
                if (targetIndex != null) {
                    ProcessJumpToExistingCode(targetIndex.Value, elements, context, ref i);
                    continue;
                }

                var targetCutBranch = elements.Select((e, index) => new { branch = e as CutBranchElement, index })
                                              .Where(x => x.branch != null)
                                              .Select(x => new {
                                                  indexOfBranch = x.index,
                                                  indexWithinBranch = BrProcessing.FindTargetIndexOrNull(element, x.branch.Elements),
                                                  elements = x.branch.Elements
                                              })
                                              .Where(x => x.indexWithinBranch != null)
                                              .FirstOrDefault();

                if (targetCutBranch == null)
                    BrProcessing.ThrowTargetNotFound(element);

                ProcessJumpToCutBranch(
                    targetCutBranch.indexOfBranch,
                    targetCutBranch.indexWithinBranch.Value,
                    targetCutBranch.elements,
                    elements,
                    context,
                    ref i
                );
            }
        }

        private void ProcessJumpToExistingCode(int targetIndex, IList<IElement> elements, InterpretationContext context, ref int currentIndex) {
            BrProcessing.EnsureNotBackward(currentIndex, targetIndex);
            var followingRange = elements.EnumerateRange(currentIndex + 1, targetIndex - (currentIndex + 1)).ToList();
            this.Apply(followingRange, context);

            ReplaceWithJumpUpTo(
                targetIndex,
                elements,
                followingRange,
                new IElement[0],
                ref currentIndex
            );
        }

        private void ProcessJumpToCutBranch(int branchIndex, int targetIndex, IList<IElement> branch, IList<IElement> elements, InterpretationContext context, ref int currentIndex) {
            var followingRange = elements.EnumerateRange(currentIndex + 1, branchIndex - (currentIndex + 1)).ToList();
            var targetRange = branch.EnumerateRange(targetIndex, branch.Count - targetIndex).ToList();
            this.Apply(followingRange, context);
            this.Apply(targetRange, context);

            ReplaceWithJumpUpTo(
                branchIndex,
                elements,
                followingRange,
                targetRange,
                ref currentIndex
            );
        }

        private static void ReplaceWithJumpUpTo(
            int replaceUpTo,
            IList<IElement> elements,
            IList<IElement> following,
            IList<IElement> target,
            ref int currentIndex
        ) {
            var jumpIfTrue = conditionalJumps[elements[currentIndex].GetOpCodeIfInstruction().Value];
            var jump = new ConditionalBranchElement(
                jumpIfTrue ? target : following,
                jumpIfTrue ? following : target
            );

            elements.RemoveRange(currentIndex, (replaceUpTo + 1) - currentIndex);
            elements.Insert(currentIndex, jump);
        }
    }
}