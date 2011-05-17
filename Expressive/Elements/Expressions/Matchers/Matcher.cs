﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Expressive.Elements.Expressions.Matchers {
    public class Matcher<T> {
        public T Target { get; private set; }
        public bool Matched { get; private set; }

        internal Matcher(T target) {
            this.Target = target;
            this.Matched = true;
        }

        public Matcher<T> AssignTo(out T value) {
            if (!this.Matched) {
                value = default(T);
                return this;
            }

            value = this.Target;
            return this;
        }

        public Matcher<T> Do(Action<T> action) {
            if (!this.Matched)
                return this;

            action(this.Target);
            return this;
        }


        public TResult Choose<TResult>(Func<TResult> ifMatched, TResult valueIfNotMatched) {
            return this.Matched ? ifMatched() : valueIfNotMatched;
        }

        public Matcher<T> MatchAs<TOther>(Func<TOther, bool> match)
            where TOther : class
        {
            return this.Match(target => {
                var typed = target as TOther;
                if (typed == null)
                    return false;

                return match(typed);
            });
        }

        public Matcher<TOther> As<TOther>() {
            if (!this.Matched || !(this.Target is TOther))
                return new Matcher<TOther>(default(TOther)) { Matched = false };

            return new Matcher<TOther>((TOther)(object)this.Target);
        }

        public Matcher<TOther> Get<TOther>(Func<T, TOther> get) {
            if (!this.Matched)
                return new Matcher<TOther>(default(TOther)) { Matched = false };

            return new Matcher<TOther>(get(this.Target));
        }

        public Matcher<T> Match(Func<T, bool> match) {
            if (!this.Matched)
                return this;

            this.Matched = this.Matched && match(this.Target);
            return this;
        }

        public Matcher<TOther> Match<TOther>(TOther value) {
            return this.Get(t => value);
        }
    }

    public static class Matcher {
        public static Matcher<T> Match<T>(T target) {
            return new Matcher<T>(target);
        }
    }
}
