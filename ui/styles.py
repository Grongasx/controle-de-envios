"""
Estilos modernos e paleta de cores para o Controle de Envios (PyQt6).
Visual Dark Moderno com alto contraste, tipografia elegante e badges coloridas.
"""

STYLESHEET = """
QMainWindow, QDialog {
    background-color: #0f172a;
    color: #f8fafc;
    font-family: 'Segoe UI', Arial, sans-serif;
    font-size: 13px;
}

QWidget {
    color: #f8fafc;
    font-family: 'Segoe UI', Arial, sans-serif;
}

/* Cards e Paineis */
QFrame#cardMetrics {
    background-color: #1e293b;
    border: 1px solid #334155;
    border-radius: 10px;
    padding: 12px;
}

QFrame#cardDropArea {
    background-color: #1e293b;
    border: 2px dashed #475569;
    border-radius: 12px;
    padding: 16px;
}
QFrame#cardDropArea:hover {
    border-color: #3b82f6;
    background-color: #1e293b;
}

/* Títulos e Labels */
QLabel {
    color: #e2e8f0;
}
QLabel#metricTitle {
    font-size: 11px;
    font-weight: 600;
    color: #94a3b8;
    text-transform: uppercase;
}
QLabel#metricValue {
    font-size: 24px;
    font-weight: bold;
    color: #ffffff;
}

/* Botões */
QPushButton {
    background-color: #334155;
    color: #ffffff;
    border: 1px solid #475569;
    border-radius: 6px;
    padding: 8px 16px;
    font-weight: 600;
    font-size: 13px;
}
QPushButton:hover {
    background-color: #475569;
    border-color: #64748b;
}
QPushButton:pressed {
    background-color: #1e293b;
}

QPushButton#btnPrimary {
    background-color: #2563eb;
    border: 1px solid #3b82f6;
}
QPushButton#btnPrimary:hover {
    background-color: #1d4ed8;
}

QPushButton#btnSuccess {
    background-color: #059669;
    border: 1px solid #10b981;
}
QPushButton#btnSuccess:hover {
    background-color: #047857;
}

QPushButton#btnWarning {
    background-color: #d97706;
    border: 1px solid #f59e0b;
}
QPushButton#btnWarning:hover {
    background-color: #b45309;
}

QPushButton#btnDanger {
    background-color: #dc2626;
    border: 1px solid #ef4444;
}
QPushButton#btnDanger:hover {
    background-color: #b91c1c;
}

/* Inputs, ComboBox e DateEdit */
QLineEdit, QComboBox, QDateEdit, QSpinBox, QDoubleSpinBox, QTextEdit {
    background-color: #1e293b;
    border: 1px solid #334155;
    border-radius: 6px;
    padding: 7px 10px;
    color: #ffffff;
    selection-background-color: #2563eb;
}
QLineEdit:focus, QComboBox:focus, QDateEdit:focus, QTextEdit:focus {
    border-color: #3b82f6;
    background-color: #0f172a;
}

QComboBox::drop-down {
    border: none;
    width: 24px;
}
QComboBox QAbstractItemView {
    background-color: #1e293b;
    border: 1px solid #334155;
    color: #ffffff;
    selection-background-color: #2563eb;
    outline: none;
}

/* Tabelas */
QTableWidget {
    background-color: #1e293b;
    border: 1px solid #334155;
    border-radius: 8px;
    gridline-color: #283548;
    color: #f1f5f9;
    selection-background-color: #1e3a8a;
    selection-color: #ffffff;
}
QHeaderView::section {
    background-color: #0f172a;
    color: #94a3b8;
    font-weight: 600;
    padding: 8px 10px;
    border: none;
    border-bottom: 2px solid #334155;
    text-transform: uppercase;
    font-size: 11px;
}
QTableWidget::item {
    padding: 8px 10px;
}
QTableWidget::item:selected {
    background-color: #1e3a8a;
}

/* ScrollBars */
QScrollBar:vertical {
    background: #0f172a;
    width: 10px;
    margin: 0px;
}
QScrollBar::handle:vertical {
    background: #334155;
    min-height: 20px;
    border-radius: 5px;
}
QScrollBar::handle:vertical:hover {
    background: #475569;
}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
    height: 0px;
}

/* Radio buttons e Checkbox */
QRadioButton, QCheckBox {
    color: #f8fafc;
    spacing: 8px;
}
QRadioButton::indicator, QCheckBox::indicator {
    width: 16px;
    height: 16px;
    border-radius: 8px;
    border: 2px solid #64748b;
    background: #1e293b;
}
QRadioButton::indicator:checked {
    background: #3b82f6;
    border-color: #60a5fa;
}
QCheckBox::indicator:checked {
    background: #3b82f6;
    border-color: #60a5fa;
    border-radius: 4px;
}
"""

def get_badge_style(status: str) -> str:
    s = (status or "").lower()
    if "entregue" in s:
        # Emerald
        return "background-color: #064e3b; color: #34d399; border: 1px solid #059669; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    elif "saiu para entrega" in s:
        # Purple
        return "background-color: #3b0764; color: #c084fc; border: 1px solid #7e22ce; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    elif "aguardando retirada" in s:
        # Amber
        return "background-color: #451a03; color: #fbbf24; border: 1px solid #d97706; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    elif "trânsito" in s or "transito" in s:
        # Blue
        return "background-color: #172554; color: #60a5fa; border: 1px solid #2563eb; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    elif "pré-postagem" in s or "pre-postagem" in s:
        # Indigo
        return "background-color: #1e1b4b; color: #818cf8; border: 1px solid #4f46e5; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    elif "alerta" in s or "retido" in s or "problema" in s or "devolvido" in s:
        # Red
        return "background-color: #450a0a; color: #f87171; border: 1px solid #dc2626; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    elif "postado" in s:
        # Cyan
        return "background-color: #083344; color: #22d3ee; border: 1px solid #0891b2; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
    else:
        # Slate / Pendente
        return "background-color: #1e293b; color: #94a3b8; border: 1px solid #475569; border-radius: 12px; padding: 3px 10px; font-weight: bold; font-size: 11px;"
