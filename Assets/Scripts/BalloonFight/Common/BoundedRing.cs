using NetworkCourse.BalloonFight.Simulation;
using System;
using System.Diagnostics;

namespace NetworkCourse.BalloonFight.Common
{
    public sealed class BoundedRing<T>
    {
        private readonly T[] _items;
        private int _head;
        private int _count;

        public BoundedRing(int capacity)
        {
            _items = new T[capacity > 0 ? capacity : 1];
        }

        public int Count => _count;
        public int Capacity => _items.Length;

        // Reset
        public void Clear()
        {
            for (int index = 0; index < _count; index++)
            {
                _items[(_head + index) % _items.Length] = default;
            }

            _head = 0;
            _count = 0;
        }

        // Ajouter la commande du tick owner. Retourne 'false' si plein
        public bool TryEnqueue(in T item)
        {
            if (_count >= _items.Length) return false;

            int tail = (_head + _count) % _items.Length;
            _items[tail] = item;
            _count++;
            return true;
        }

        // Ça purge tout ce qui est ack server
        // ack veut dire acknowledgement server c'est à dire ce qui a été 'accusé de réception'
        // Dans le cas du Balloon Fight que nous réalisons là, cela veut dire que
        // tous les inputs qui ont été consommés sont retiré de la file
        public bool TryDequeue(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            _count--;
            return true;
        }

        // Celui-ci est similaire à TryDequeue à la différence qu'il ne retire pas du tableau.
        // On peut donc vérifier quel est l'élément le plus ancien sans risque de modifier le tableau
        public bool TryPeek(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _items[_head];
            return true;
        }

        // InputBatch.TryCreateNewest copie les 8 plus récentess
        // InputHistory.TryGet cherche par tick
        public bool TryGetAt(int logicalIndex, out T item)
        {
            if (logicalIndex < 0 || logicalIndex >= _count)
            {
                item = default;
                return false;
            }

            item = _items[(_head + logicalIndex) % _items.Length];
            return true;
        }
    }
}