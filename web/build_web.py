# Builds docs/index.html (GitHub Pages) from web/ui.html + web/engine.js
import pathlib
root = pathlib.Path(__file__).resolve().parent.parent
ui = (root / "web" / "ui.html").read_text(encoding="utf-8")
eng = (root / "web" / "engine.js").read_text(encoding="utf-8")
(root / "docs" / "index.html").write_text(ui.replace("/*ENGINE*/", eng), encoding="utf-8")
print("docs/index.html written")
