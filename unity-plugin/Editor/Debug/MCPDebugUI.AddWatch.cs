using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal partial class MCPDebugUI
    {
        private DropdownField _componentDropdown;
        private DropdownField _fieldDropdown;
        private GameObject _addWatchGo;

        private VisualElement BuildAddWatch()
        {
            var form = new VisualElement();
            form.AddToClassList("add-watch-form");

            var header = new Label("Add Watch");
            header.AddToClassList("section-header");
            form.Add(header);

            var goField = new ObjectField("Object") { objectType = typeof(GameObject) };
            goField.AddToClassList("add-watch-go");

            _componentDropdown = new DropdownField("Component", new List<string> { "–" }, 0);
            _fieldDropdown = new DropdownField("Field", new List<string> { "–" }, 0);

            var conditionInput = new TextField("Condition") { value = "" };
            conditionInput.tooltip = "Optional: < 10, == true, etc.";

            var actionDropdown = new DropdownField("Action",
                new List<string> { "log", "pause" }, 0);

            var addBtn = new Button(() => SubmitAddWatch(conditionInput.value, actionDropdown.value))
                { text = "Add Watch" };
            addBtn.AddToClassList("add-watch-btn");

            goField.RegisterValueChangedCallback(e => {
                _addWatchGo = e.newValue as GameObject;
                RefreshComponentDropdown(_addWatchGo);
            });

            _componentDropdown.RegisterValueChangedCallback(e =>
                RefreshFieldDropdown(_addWatchGo, e.newValue));

            form.Add(goField);
            form.Add(_componentDropdown);
            form.Add(_fieldDropdown);
            form.Add(conditionInput);
            form.Add(actionDropdown);
            form.Add(addBtn);
            return form;
        }

        private void SubmitAddWatch(string condition, string action)
        {
            if (_addWatchGo == null) return;
            var comp = _componentDropdown?.value ?? "–";
            var field = _fieldDropdown?.value ?? "–";
            if (comp == "–" || field == "–") return;

            var path = ComponentSerializer.GetPath(_addWatchGo);
            WatchRegistry.Add(path, comp, field, condition, action);
        }

        private void RefreshComponentDropdown(GameObject go)
        {
            var names = new List<string> { "–" };
            if (go != null)
                names.AddRange(go.GetComponents<Component>().Select(c => c.GetType().Name));
            if (_componentDropdown != null)
            {
                _componentDropdown.choices = names;
                _componentDropdown.index = 0;
            }
            if (_fieldDropdown != null)
            {
                _fieldDropdown.choices = new List<string> { "–" };
                _fieldDropdown.index = 0;
            }
        }

        private void RefreshFieldDropdown(GameObject go, string componentName)
        {
            var names = new List<string> { "–" };
            if (go != null && componentName != "–")
            {
                var comp = go.GetComponent(componentName);
                if (comp != null)
                    names.AddRange(comp.GetType()
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(f => f.IsPublic || f.IsDefined(typeof(UnityEngine.SerializeField), false))
                        .Select(f => f.Name));
            }
            if (_fieldDropdown != null)
            {
                _fieldDropdown.choices = names;
                _fieldDropdown.index = 0;
            }
        }
    }
}
