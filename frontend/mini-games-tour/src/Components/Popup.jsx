import React from 'react';
import './styles.css';

const Popup = ({children, className = '' }) => {
  return (
    <div className={`popup-overlay ${className}`}>
      <div className="popup-body">{children}</div>
    </div>
  );
};

export default Popup;