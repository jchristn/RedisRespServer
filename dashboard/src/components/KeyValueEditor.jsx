import './KeyValueEditor.css';

export default function KeyValueEditor({ pairs, onChange, keyPlaceholder = 'Key', valuePlaceholder = 'Value' }) {
  const handlePairChange = (index, field, value) => {
    const newPairs = [...pairs];
    newPairs[index] = { ...newPairs[index], [field]: value };
    onChange(newPairs);
  };

  const handleAddPair = () => {
    onChange([...pairs, { key: '', value: '' }]);
  };

  const handleRemovePair = (index) => {
    onChange(pairs.filter((_, i) => i !== index));
  };

  return (
    <div className="kv-editor">
      {pairs.map((pair, index) => (
        <div key={index} className="kv-row">
          <input
            type="text"
            className="input kv-key"
            placeholder={keyPlaceholder}
            value={pair.key}
            onChange={(e) => handlePairChange(index, 'key', e.target.value)}
          />
          <input
            type="text"
            className="input kv-value"
            placeholder={valuePlaceholder}
            value={pair.value}
            onChange={(e) => handlePairChange(index, 'value', e.target.value)}
          />
          <button
            type="button"
            className="btn-icon kv-remove"
            onClick={() => handleRemovePair(index)}
            title="Remove"
          >
            &times;
          </button>
        </div>
      ))}
      <button type="button" className="btn btn-secondary btn-sm" onClick={handleAddPair}>
        + Add Field
      </button>
    </div>
  );
}
