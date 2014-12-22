using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Redwood.Framework.Controls;
using Redwood.Framework.Parser;
using Redwood.Framework.Runtime;

namespace Redwood.Framework.Binding
{
    /// <summary>
    /// Finds the command to execute in the viewmodel using the path and command expression.
    /// </summary>
    public class CommandResolver
    {

        /// <summary>
        /// Resolves the command called on the RedwoodControl.
        /// </summary>
        public Action GetFunction(RedwoodControl targetControl, object viewModel, string[] path, string command)
        {
            // resolve the path in the view model
            List<object> hierarchy;
            var pathObject = ResolveViewModelPath(viewModel, path, out hierarchy);

            // find the function
            var tree = CSharpSyntaxTree.ParseText(command, new CSharpParseOptions(LanguageVersion.CSharp5, DocumentationMode.Parse, SourceCodeKind.Interactive));
            var expr = tree.EnsureSingleExpression();
            var node = expr.ChildNodes().First() as InvocationExpressionSyntax;
            if (node == null)
            {
                throw new ParserException("The expression in command must be a method call!");
            }
            MethodInfo method;
            object target;
            if (targetControl != null)
            {
                // the function is invoked on the control object
                method = FindMethodOnControl(targetControl, node);
                target = targetControl;
            }
            else
            {
                // the function is invoked on the viewmodel
                method = FindMethodOnViewModel(viewModel, pathObject, hierarchy, node, out target);
            }
            
            // parse arguments
            var arguments = EvaluateCommandArguments(viewModel, node, pathObject, hierarchy);

            // return the delegate for further invoke
            return () => method.Invoke(target, arguments);
        }


        /// <summary>
        /// Resolves the command called on the ViewModel.
        /// </summary>
        public Action GetFunction(object viewModel, string[] path, string command)
        {
            return GetFunction(null, viewModel, path, command);
        }




        /// <summary>
        /// Evaluates the command arguments.
        /// </summary>
        private static object[] EvaluateCommandArguments(object viewModel, InvocationExpressionSyntax node, object pathObject, List<object> hierarchy)
        {
            var arguments = node.ArgumentList.Arguments.Select(a =>
            {
                var evaluator = new ExpressionEvaluationVisitor()
                {
                    Root = viewModel,
                    DataContext = pathObject,
                    Hierarchy = new Stack<object>(hierarchy)
                };
                return evaluator.Visit(a);
            }).ToArray();
            return arguments;
        }

        /// <summary>
        /// Finds the method on view model.
        /// </summary>
        private static MethodInfo FindMethodOnViewModel(object viewModel, object pathObject, List<object> hierarchy, InvocationExpressionSyntax node, out object target)
        {
            MethodInfo method;
            var methodEvaluator = new ExpressionEvaluationVisitor()
            {
                Root = viewModel,
                DataContext = pathObject,
                AllowMethods = true,
                Hierarchy = new Stack<object>(hierarchy)
            };
            method = methodEvaluator.Visit(node.Expression) as MethodInfo;
            if (method == null)
            {
                throw new Exception("The path was not found!");
            }
            target = methodEvaluator.Target;
            return method;
        }

        /// <summary>
        /// Finds the method on control.
        /// </summary>
        private static MethodInfo FindMethodOnControl(RedwoodControl targetControl, InvocationExpressionSyntax node)
        {
            MethodInfo method;
            var methods = targetControl.GetType().GetMethods().Where(m => m.Name == node.Expression.ToString()).ToList();
            if (methods.Count == 0)
            {
                throw new Exception(string.Format("The control '{0}' does not have a function '{1}'!", targetControl.GetType().FullName, node.Expression));
            }
            else if (methods.Count > 1)
            {
                throw new Exception(string.Format("The control '{0}' has more than one function called '{1}'! Overloading in {{controlCommand: ...}} binding is not yet supported!", targetControl.GetType().FullName, node.Expression));
            }
            method = methods[0];
            return method;
        }

        /// <summary>
        /// Resolves the path in the view model and returns the target object.
        /// </summary>
        private object ResolveViewModelPath(object viewModel, string[] path, out List<object> hierarchy)
        {
            object pathObject = viewModel;

            var pathExpression = GetFullCommandPath(path);
            if (!string.IsNullOrEmpty(pathExpression))
            {
                var pathTree = CSharpSyntaxTree.ParseText(pathExpression, new CSharpParseOptions(LanguageVersion.CSharp5, DocumentationMode.Parse, SourceCodeKind.Interactive));
                var pathExpr = pathTree.EnsureSingleExpression();

                // find the target on which the function is called
                var pathExpressionEvaluator = new ExpressionEvaluationVisitor { Root = viewModel, DataContext = viewModel };
                pathObject = pathExpressionEvaluator.Visit(pathExpr);
                hierarchy = pathExpressionEvaluator.Hierarchy.Reverse().ToList();
            }
            else
            {
                hierarchy = new List<object>();
            }
            return pathObject;
        }


        /// <summary>
        /// Gets the full command path.
        /// </summary>
        private string GetFullCommandPath(IEnumerable<string> path)
        {
            var pathString = new StringBuilder();
            foreach (var fragment in path)
            {
                if (pathString.Length > 0 && !fragment.StartsWith("["))
                {
                    pathString.Append(".");
                }
                pathString.Append(fragment);
            }
            return pathString.ToString();
        }
    }
}