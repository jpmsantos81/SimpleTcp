namespace Jpmsantos81.SimpleTcp
{
    internal class EventController
    {
        internal readonly List<Action> _remover = new();

        internal void Vincular(Action registrar, Action remover)
        {
            registrar();
            _remover.Add(remover);
        }

        internal void Desvincular()
        {
            foreach (var r in _remover)
                r();

            _remover.Clear();
        }
    }
}