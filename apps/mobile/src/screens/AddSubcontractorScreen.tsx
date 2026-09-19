import React, { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput } from 'react-native';
import { Picker } from '@react-native-picker/picker';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours, subcontractorStatuses } from '../theme';
import type { SubsStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<SubsStackParamList, 'AddSubcontractor'>;

export function AddSubcontractorScreen({ navigation }: Props) {
  const { request } = useAuth();
  const [name, setName] = useState('');
  const [contactName, setContactName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [status, setStatus] = useState('active');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      await request('/api/subcontractors', {
        method: 'POST',
        body: {
          name: name.trim(),
          contactName: contactName.trim() || null,
          email: email.trim() || null,
          phone: phone.trim() || null,
          status
        }
      });
      navigation.goBack();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollView style={styles.flex} contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.label}>Company name</Text>
      <TextInput style={styles.input} value={name} onChangeText={setName} />
      <Text style={styles.label}>Contact</Text>
      <TextInput style={styles.input} value={contactName} onChangeText={setContactName} />
      <Text style={styles.label}>Email</Text>
      <TextInput style={styles.input} value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" />
      <Text style={styles.label}>Phone</Text>
      <TextInput style={styles.input} value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
      <Text style={styles.label}>Status</Text>
      <Picker selectedValue={status} onValueChange={setStatus} style={styles.picker}>
        {subcontractorStatuses.map((item) => (
          <Picker.Item key={item.value} label={item.label} value={item.value} />
        ))}
      </Picker>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <Pressable style={[styles.button, busy && styles.disabled]} onPress={onSubmit} disabled={busy}>
        <Text style={styles.buttonText}>{busy ? 'Saving…' : 'Save subcontractor'}</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 20 },
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
  picker: { backgroundColor: colours.white, marginBottom: 14 },
  button: { backgroundColor: colours.navy, borderRadius: 10, padding: 14, alignItems: 'center' },
  disabled: { opacity: 0.6 },
  buttonText: { color: colours.white, fontWeight: '700' },
  error: { color: colours.red, marginBottom: 8 }
});
