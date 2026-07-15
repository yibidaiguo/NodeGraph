# NodeGraph

NodeGraph is a modular graph editing framework for Unity 6. It includes a reusable node editor plus optional Dialogue, Task, and State Machine modules and samples.

## Requirements

- Unity 6 (`6000.0` or newer)
- Git 2.14 or newer available on `PATH`

## Install

In Unity, open **Window > Package Management > Package Manager**, choose **Install package from git URL**, and enter:

```text
https://github.com/yibidaiguo/NodeGraph.git?path=/Packages/com.graphtest.nodeeditor#v0.0.1
```

Then open **Tools > NodeGraph > Manager** to install the optional modules from the same repository and revision.

## Packages

| Package | Purpose |
| --- | --- |
| `com.graphtest.nodeeditor` | Graph runtime, editor framework, and module manager |
| `com.graphtest.dialogue` | Dialogue graph runtime and editor |
| `com.graphtest.task` | Task graph runtime and editor |
| `com.graphtest.statemachine` | State machine graph runtime and editor |
| `com.graphtest.*.samples` | Optional sample content for each domain |

Detailed usage and extension documentation is included in each package directory under `Packages/`.

Maintainers publish new versions through the Unity one-click release window documented in
[`Development/Publishing/README.md`](Development/Publishing/README.md).

## License

MIT. See [LICENSE](LICENSE).
