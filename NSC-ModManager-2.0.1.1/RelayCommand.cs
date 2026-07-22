using System;
using System.Windows.Input;

namespace NSC_ModManager
{
    /// <summary>
    /// Simple ICommand implementation used throughout the ViewModel layer (1000+
    /// call sites). Unchanged from before the WPF removal -- ICommand itself is a
    /// portable BCL type (System.ObjectModel.dll), not a WPF-only type, so this
    /// class needed no changes, just a new home now that App.xaml.cs is gone.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }
}
