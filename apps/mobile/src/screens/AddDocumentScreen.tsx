import React, { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput } from 'react-native';
import { Picker } from '@react-native-picker/picker';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours, documentTypes } from '../theme';

type Props = NativeStackScreenProps<{ AddDocument: { subcontractorId: string; name: string } }, 'AddDocument'>;

export function AddDocumentScreen({ navigation, route }: Props) {
  const { request } = useAuth();
  const [type, setType] = useState('employersLiability');
  const [title, setTitle] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [fileName, setFileName] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      await request(`/api/subcontractors/${route.params.subcontractorId}/documents`, {
        method: 'POST',
        body: {
          type,
          title: title.trim(),
          expiryDate: expiryDate.trim() || null,
          fileName: fileName.trim() || null,
          contentType: fileName.toLowerCase().endsWith('.pdf') ? 'application/pdf' : null,
          notes: notes.trim() || null
        }
      });
      navigation.goBack();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save document.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollView style={styles.flex} contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.lede}>File bytes stay in your document store later. MVP records type, expiry and file metadata.</Text>
      <Text style={styles.label}>Type</Text>
      <Picker selectedValue={type} onValueChange={setType} style={styles.picker}>
        {documentTypes.map((item) => (
          <Picker.Item key={item.value} label={item.label} value={item.value} />
        ))}
      </Picker>
      <Text style={styles.label}>Title</Text>
      <TextInput style={styles.input} value={title} onChangeText={setTitle} placeholder="EL certificate 2026/27" />
      <Text style={styles.label}>Expiry (YYYY-MM-DD)</Text>
      <TextInput style={styles.input} value={expiryDate} onChangeText={setExpiryDate} placeholder="2027-03-31" />
      <Text style={styles.label}>File name</Text>
      <TextInput style={styles.input} value={fileName} onChangeText={setFileName} placeholder="el-certificate.pdf" />
      <Text style={styles.label}>Notes</Text>
      <TextInput style={[styles.input, styles.multiline]} value={notes} onChangeText={setNotes} multiline />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <Pressable style={[styles.button, busy && styles.disabled]} onPress={onSubmit} disabled={busy}>
        <Text style={styles.buttonText}>{busy ? 'Saving…' : 'Save document'}</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 20 },
  lede: { color: colours.muted, marginBottom: 16, lineHeight: 20 },
  label: { color: colours.muted, fontWeight: '600', marginBottom: 6 },
  input: {
    backgroundColor: colours.white,
    borderColor: colours.line,
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 16,
    marginBottom: 14,
    color: colours.ink
  },
  multiline: { minHeight: 90, textAlignVertical: 'top' },
  picker: { backgroundColor: colours.white, marginBottom: 14 },
  button: { backgroundColor: colours.navy, borderRadius: 10, padding: 14, alignItems: 'center' },
  disabled: { opacity: 0.6 },
  buttonText: { color: colours.white, fontWeight: '700' },
  error: { color: colours.red, marginBottom: 8 }
});
