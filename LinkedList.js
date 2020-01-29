export class LinkedList {

    constructor() {
        if (LinkedList.nodePool === undefined) {
            LinkedList.nodePool = {
                last: null,
                count: 0
            };
        }
        this.first = null;
        this.last = null;
        this.count = 0;
    }

    add(obj) {
        var node = LinkedList.nodePool.last;
        if (node === null) {
            node = new LinkedListNode();
        } else {
            LinkedList.nodePool.last = node.prev;
            LinkedList.nodePool.count--;
        }

        node.value = obj;
        // if (obj === Object(obj)) {
        //     var funcDispose = obj.dispose;
        //     obj.dispose = function () {
        //         if (funcDispose) funcDispose();
        //         if (node.list === null) return;
        //         node.list.remove(node);
        //     }
        // }

        if (this.last) {
            this.last.next = node;
        }
        node.prev = this.last;
        this.last = node;
        this.count++;
        if (this.first === null) {
            this.first = node;
        }
        node.list = this;
        return node;
    }

    remove(node) {
        if (node.next !== null) {
            node.next.prev = node.prev;
        }
        if (node.prev !== null) {
            node.prev.next = node.next;
        }
        if (this.last === node) {
            this.last = node.prev;
        }
        if (this.first === node) {
            this.first = node.next;
        }
        node.list = null;
        node.value = null;
        node.prev = LinkedList.nodePool.last;
        LinkedList.nodePool.last = node;
        LinkedList.nodePool.count++;
        node.next = null;
        this.count--;
    }

}

export class LinkedListNode {
    constructor() {
        this.list = null;
        this.value = null;
        this.prev = null;
        this.next = null;
    }

    dispose() {
        if (this.list === null) return;
        this.list.remove(this);
    }

}
